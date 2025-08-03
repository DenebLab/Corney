using System;
using MediatR;

namespace Corney.Features.App;

public record MinuteExecutionNotification(DateTime ExecutionTime) : INotification;

public record AppStartingEvent : INotification;
public record AppStartedEvent : INotification;