using DrMW.EventBus.Core.BaseModels;

namespace TestBus.Models.TestQueEvents;

public class TestQueIntegrationEvent : IntegrationEvent
{
    public int? Id { get; set; }
}