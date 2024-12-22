using DrMW.EventBus.Core.BaseModels;

namespace ConsoleAppTest.Models.TestQueEvents;

public class TestQueIntegrationEvent : IntegrationEvent
{
    public int? Id { get; set; }
}