// Parse heap allocation/deallocation events, tracking memory lifetime
using System.Runtime.InteropServices;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Symbols;

namespace ETLExport;

class HeapEventParser
{
    private static readonly Guid HeapProviderId = new("222962ab-6180-4b88-a825-346b75f2a24a");

    private readonly List<HeapAllocation> _heapAllocations = [];
    private readonly Dictionary<ulong, Dictionary<ulong, HeapAllocation>> _heaps = [];
    private readonly IPendingResult<IStackDataSource> _pendingStackDataSource;

    public HeapEventParser(ITraceProcessor trace)
    {
        _pendingStackDataSource = trace.UseStacks();
        trace.Use(new[] { HeapProviderId }, Process);
    }

    public List<HeapAllocation> GetAllocations()
    {
        foreach (var heap in _heaps.Values)
            _heapAllocations.AddRange(heap.Values);
        _heaps.Clear();
        return _heapAllocations;
    }

    public void Process(EventContext eventContext)
    {
        var e = eventContext.Event.AsClassicEvent;
        switch (e.Id)
        {
            case 33: ProcessAlloc(e); break;
            case 34: ProcessRealloc(e); break;
            case 35: ProcessDestroy(e); break;
            case 36: ProcessFree(e); break;
        }
    }

    private void ProcessAlloc(ClassicEvent e)
    {
        if (e.Is32Bit)
        {
            var data = MemoryMarshal.Read<HeapAlloc32>(e.Data);
            RecordAlloc(e, data.HeapHandle, data.Address, data.Size);
        }
        else
        {
            var data = MemoryMarshal.Read<HeapAlloc64>(e.Data);
            RecordAlloc(e, data.HeapHandle, data.Address, data.Size);
        }
    }

    private void ProcessFree(ClassicEvent e)
    {
        if (e.Is32Bit)
        {
            var data = MemoryMarshal.Read<HeapFree32>(e.Data);
            RecordFree(e, data.HeapHandle, data.Address);
        }
        else
        {
            var data = MemoryMarshal.Read<HeapFree64>(e.Data);
            RecordFree(e, data.HeapHandle, data.Address);
        }
    }

    private void ProcessRealloc(ClassicEvent e)
    {
        if (e.Is32Bit)
        {
            var data = MemoryMarshal.Read<HeapRealloc32>(e.Data);
            // Moving reallocs generate separate alloc/free events; only handle same-address resizing
            if (data.OldAddress == data.NewAddress)
            {
                RecordFree(e, data.HeapHandle, data.OldAddress);
                RecordAlloc(e, data.HeapHandle, data.NewAddress, data.NewSize);
            }
        }
        else
        {
            var data = MemoryMarshal.Read<HeapRealloc64>(e.Data);
            if (data.OldAddress == data.NewAddress)
            {
                RecordFree(e, data.HeapHandle, data.OldAddress);
                RecordAlloc(e, data.HeapHandle, data.NewAddress, data.NewSize);
            }
        }
    }

    private void ProcessDestroy(ClassicEvent e)
    {
        var heap = e.Is32Bit ? MemoryMarshal.Read<uint>(e.Data) : MemoryMarshal.Read<ulong>(e.Data);

        if (_heaps.TryGetValue(heap, out var allocs))
        {
            foreach (var alloc in allocs.Values)
            {
                alloc.FreeThreadId = e.ThreadId;
                alloc.FreeTime = e.Timestamp;
                _heapAllocations.Add(alloc);
            }
            _heaps.Remove(heap);
        }
    }

    private Dictionary<ulong, HeapAllocation> GetHeap(ulong handle)
    {
        if (!_heaps.TryGetValue(handle, out var heap))
            _heaps[handle] = heap = new Dictionary<ulong, HeapAllocation>();
        return heap;
    }

    private void RecordAlloc(ClassicEvent e, ulong heap, ulong addr, ulong size)
    {
        GetHeap(heap)[addr] = new HeapAllocation(
            e.ProcessId ?? 0, heap, new AddressRange(new Address(addr), checked((long)size)),
            e.ThreadId ?? 0, e.Timestamp, _pendingStackDataSource);
    }

    private void RecordFree(ClassicEvent e, ulong heap, ulong addr)
    {
        var allocs = GetHeap(heap);
        if (allocs.TryGetValue(addr, out var alloc))
        {
            alloc.FreeThreadId = e.ThreadId;
            alloc.FreeTime = e.Timestamp;
            allocs.Remove(addr);
            _heapAllocations.Add(alloc);
        }
    }

    private readonly record struct HeapAlloc32(uint HeapHandle, uint Size, uint Address);
    private readonly record struct HeapAlloc64(ulong HeapHandle, ulong Size, ulong Address);
    private readonly record struct HeapFree32(uint HeapHandle, uint Address);
    private readonly record struct HeapFree64(ulong HeapHandle, ulong Address);
    private readonly record struct HeapRealloc32(uint HeapHandle, uint NewAddress, uint OldAddress, uint NewSize, uint OldSize);
    private readonly record struct HeapRealloc64(ulong HeapHandle, ulong NewAddress, ulong OldAddress, ulong NewSize, ulong OldSize);
}

class HeapAllocation(int processId, ulong heapHandle, AddressRange addressRange,
    int allocThreadId, TraceTimestamp allocTime, IPendingResult<IStackDataSource> pendingStackDataSource)
{
    public int ProcessId { get; } = processId;
    public ulong HeapHandle { get; } = heapHandle;
    public AddressRange AddressRange { get; } = addressRange;
    public int AllocThreadId { get; } = allocThreadId;
    public TraceTimestamp AllocTime { get; } = allocTime;
    public IStackSnapshot AllocStack => pendingStackDataSource.Result.GetStack(AllocTime, AllocThreadId);
    public int? FreeThreadId { get; set; }
    public TraceTimestamp? FreeTime { get; set; }
}
