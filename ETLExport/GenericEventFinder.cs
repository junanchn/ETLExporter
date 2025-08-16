using System;
using System.Collections.Generic;
using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Events;

public class GenericEventFilter
{
    public Guid? ProviderId { get; set; }
    public int? Task { get; set; }
    public int? Opcode { get; set; }
    public string ProviderName { get; set; }
    public string TaskName { get; set; }
    public string OpcodeName { get; set; }
}

public class GenericEventFinder
{
    private readonly GenericEventFilter _filter;
    private readonly IPendingResult<IGenericEventDataSource> _pendingGenericEvents;

    public GenericEventFinder(GenericEventFilter filter, ITraceProcessor traceProcessor)
    {
        _filter = filter;
        if (filter.ProviderId.HasValue)
            _pendingGenericEvents = traceProcessor.UseGenericEvents(filter.ProviderId.Value);
        else
            _pendingGenericEvents = traceProcessor.UseGenericEvents();
    }

    public List<IGenericEvent> FindEvents()
    {
        var results = new List<IGenericEvent>();
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
