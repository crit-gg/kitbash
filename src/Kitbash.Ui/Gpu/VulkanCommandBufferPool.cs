using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Kitbash.Ui.Gpu;

/// <summary>
/// One pool for the thread that records. A buffer goes back on the free list once its
/// fence says the queue is done with it, so a frame never waits on the previous one.
/// </summary>
public sealed unsafe class VulkanCommandBufferPool : IDisposable
{
    private readonly Vk _api;
    private readonly Device _device;
    private readonly Queue _queue;
    private readonly CommandPool _pool;
    private readonly List<Frame> _submitted = new();
    private readonly Stack<Frame> _free = new();
    private readonly List<(Fence[] After, Action Free)> _deferred = new();
    private readonly Lock _lock = new();

    public VulkanCommandBufferPool(Vk api, Device device, Queue queue, uint queueFamilyIndex)
    {
        _api = api;
        _device = device;
        _queue = queue;

        var info = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndex,
        };

        _api.CreateCommandPool(_device, in info, null, out _pool).ThrowOnError("vkCreateCommandPool");
    }

    /// <summary>
    /// Takes back every buffer the GPU has finished with. Call once per frame before
    /// recording, never inside it, since nothing here waits.
    /// </summary>
    public void Recycle()
    {
        List<Action>? due = null;

        lock (_lock)
        {
            for (var i = _submitted.Count - 1; i >= 0; i--)
            {
                var frame = _submitted[i];

                if (_api.GetFenceStatus(_device, frame.Fence) != Result.Success)
                {
                    continue;
                }

                _submitted.RemoveAt(i);
                _api.ResetCommandBuffer(frame.Buffer, 0);
                _free.Push(frame);
            }

            for (var i = _deferred.Count - 1; i >= 0; i--)
            {
                if (!Done(_deferred[i].After))
                {
                    continue;
                }

                (due ??= new List<Action>()).Add(_deferred[i].Free);
                _deferred.RemoveAt(i);
            }
        }

        if (due == null)
        {
            return;
        }

        foreach (var free in due)
        {
            free();
        }
    }

    /// <summary>
    /// Runs an action once the queue has finished every buffer submitted before this call.
    /// A fence reused by a later submission only delays it, since one queue finishes in the
    /// order it was given work.
    /// </summary>
    public void Defer(Action free)
    {
        lock (_lock)
        {
            if (_submitted.Count > 0)
            {
                _deferred.Add((_submitted.Select(frame => frame.Fence).ToArray(), free));
                return;
            }
        }

        free();
    }

    private bool Done(Fence[] fences)
    {
        foreach (var fence in fences)
        {
            if (_api.GetFenceStatus(_device, fence) != Result.Success)
            {
                return false;
            }
        }

        return true;
    }

    public Recording Begin()
    {
        Frame frame;

        lock (_lock)
        {
            frame = _free.Count > 0 ? _free.Pop() : Allocate();
        }

        var begin = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        _api.BeginCommandBuffer(frame.Buffer, in begin).ThrowOnError("vkBeginCommandBuffer");
        return new Recording(this, frame);
    }

    private Frame Allocate()
    {
        var allocate = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _pool,
            CommandBufferCount = 1,
            Level = CommandBufferLevel.Primary,
        };

        _api.AllocateCommandBuffers(_device, in allocate, out var buffer)
            .ThrowOnError("vkAllocateCommandBuffers");

        var fenceInfo = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        _api.CreateFence(_device, in fenceInfo, null, out var fence).ThrowOnError("vkCreateFence");

        return new Frame(buffer, fence);
    }

    private void Submit(Frame frame, ReadOnlySpan<Semaphore> wait, ReadOnlySpan<Semaphore> signal)
    {
        _api.EndCommandBuffer(frame.Buffer).ThrowOnError("vkEndCommandBuffer");

        Span<PipelineStageFlags> stages = stackalloc PipelineStageFlags[wait.Length];
        stages.Fill(PipelineStageFlags.AllCommandsBit);

        fixed (Semaphore* pWait = wait)
        fixed (Semaphore* pSignal = signal)
        fixed (PipelineStageFlags* pStages = stages)
        {
            var buffer = frame.Buffer;

            var submit = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = (uint)wait.Length,
                PWaitSemaphores = pWait,
                PWaitDstStageMask = pStages,
                CommandBufferCount = 1,
                PCommandBuffers = &buffer,
                SignalSemaphoreCount = (uint)signal.Length,
                PSignalSemaphores = pSignal,
            };

            var fence = frame.Fence;

            lock (_lock)
            {
                _api.ResetFences(_device, 1, in fence).ThrowOnError("vkResetFences");
                _api.QueueSubmit(_queue, 1, in submit, fence).ThrowOnError("vkQueueSubmit");
                _submitted.Add(frame);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var frame in _submitted.Concat(_free))
            {
                var fence = frame.Fence;
                _api.WaitForFences(_device, 1, in fence, true, ulong.MaxValue);
                _api.DestroyFence(_device, fence, null);
            }

            foreach (var (_, free) in _deferred)
            {
                free();
            }

            _deferred.Clear();

            _submitted.Clear();
            _free.Clear();
            _api.DestroyCommandPool(_device, _pool, null);
        }
    }

    public readonly record struct Frame(CommandBuffer Buffer, Fence Fence);

    /// <summary>
    /// An open command buffer. Submit exactly once, which ends the recording and puts the
    /// buffer back in flight.
    /// </summary>
    public readonly struct Recording
    {
        private readonly VulkanCommandBufferPool _owner;
        private readonly Frame _frame;

        internal Recording(VulkanCommandBufferPool owner, Frame frame)
        {
            _owner = owner;
            _frame = frame;
        }

        public CommandBuffer Buffer => _frame.Buffer;

        /// <summary>Signalled once the queue is done with this buffer. Valid until it is reused.</summary>
        public Fence Fence => _frame.Fence;

        public void Submit(ReadOnlySpan<Semaphore> wait = default, ReadOnlySpan<Semaphore> signal = default)
            => _owner.Submit(_frame, wait, signal);
    }
}
