using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Events;

namespace ETLExport;

public record GenericEventFilter
{
    public Guid? ProviderId { get; init; }
    public int? Task { get; init; }
    public int? Opcode { get; init; }
    public string? ProviderName { get; init; }
    public string? TaskName { get; init; }
    public string? OpcodeName { get; init; }
}

public class GenericEventFinder
{
    private readonly GenericEventFilter _filter;
    private readonly IPendingResult<IGenericEventDataSource> _pendingGenericEvents;

    public GenericEventFinder(GenericEventFilter filter, ITraceProcessor traceProcessor)
    {
        _filter = filter;
        _pendingGenericEvents = filter.ProviderId.HasValue
            ? traceProcessor.UseGenericEvents(filter.ProviderId.Value)
            : traceProcessor.UseGenericEvents();
    }

    public List<IGenericEvent> FindEvents()
    {
        List<IGenericEvent> results = [];
        foreach (var evt in _pendingGenericEvents.Result.Events)
        {
            if (_filter.ProviderId.HasValue && evt.ProviderId != _filter.ProviderId.Value)
                continue;
            if (_filter.Task.HasValue && evt.Task != _filter.Task.Value)
                continue;
            if (_filter.Opcode.HasValue && evt.Opcode != _filter.Opcode.Value)
                continue;
            if (!string.IsNullOrEmpty(_filter.ProviderName) && !evt.ProviderName.Equals(_filter.ProviderName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrEmpty(_filter.TaskName) && !evt.TaskName.Equals(_filter.TaskName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrEmpty(_filter.OpcodeName) && !evt.OpcodeName.Equals(_filter.OpcodeName, StringComparison.OrdinalIgnoreCase))
                continue;
            results.Add(evt);
        }
        return results;
    }
}
