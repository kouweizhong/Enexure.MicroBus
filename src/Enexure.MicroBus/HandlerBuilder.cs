﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enexure.MicroBus;

namespace Enexure.MicroBus
{
	public class HandlerBuilder : IHandlerBuilder
	{
		private readonly IHandlerActivator handlerActivator;
		private readonly IHandlerRegistar handlerRegistar;

		public HandlerBuilder(IHandlerActivator handlerActivator, IHandlerRegistar handlerRegistar)
		{
			this.handlerActivator = handlerActivator;
			this.handlerRegistar = handlerRegistar;
		}

		public ICommandHandler<TCommand> GetRunnerForCommand<TCommand>()
			where TCommand : ICommand
		{
			return GetRunnerForMessage<ICommandHandler<TCommand>, TCommand>(
				handlers => handlers.Single(),
				handler => new CommandHandlerPretendToBePipelineHandler<TCommand>(handler),
				handler => new PretendToBeCommandHandler<TCommand>(handler),
				handler => new EventPretendToBeCommandHandler<TCommand>(handler)
				);
		}

		public IEventHandler<TEvent> GetRunnerForEvent<TEvent>() 
			where TEvent : IEvent
		{
			return GetRunnerForMessage<IEventHandler<TEvent>, TEvent>(
				handlers => new PretendMultipleToBeEventHandler<TEvent>(handlers),
				handler => new EventHandlerPretendToBePipelineHandler<TEvent>(handler),
				handler => new PretendToBeEventHandler<TEvent>(handler),
				handler => new EventPretendToBeEventHandler<TEvent>(handler)
				);
		}

		public IQueryHandler<TQuery, TResult> GetRunnerForQuery<TQuery, TResult>()
			where TQuery : IQuery<TQuery, TResult>
			where TResult : IResult
		{
			return GetRunnerForMessage<IQueryHandler<TQuery, TResult>, TQuery>(
				handlers => handlers.Single(),
				handler => new QueryHandlerPretendToBePipelineHandler<TQuery, TResult>(handler), 
				handler => new PretendToBeQueryHandler<TQuery, TResult>(handler),
				handler => new EventPretendToBeQueryHandler<TQuery, TResult>(handler)
				);
		}

		private THandler GetRunnerForMessage<THandler, TMessage>(
			Func<IEnumerable<THandler>, 
			THandler> mergeHandlers, 
			Func<THandler, IPipelineHandler> makePretend, 
			Func<IPipelineHandler, THandler> makeReal,
			Func<IEventHandler<NoMatchingRegistrationEvent>, THandler> convertNoRegistrationHandler 
			)
			where TMessage : IMessage
		{
			var messageType = typeof(TMessage);
			var registration = handlerRegistar.GetRegistrationForMessage(messageType);

			if (registration == null) {
				if (typeof(TMessage) == typeof(NoMatchingRegistrationEvent)) {
					return default(THandler);
				}

				var fallbackHandler = convertNoRegistrationHandler(GetRunnerForEvent<NoMatchingRegistrationEvent>());
				if (fallbackHandler == null) {
					throw new NoRegistrationForMessage(messageType);
				}
			}

			var handlers = handlerActivator.ActivateHandlers<THandler>(registration);

			var innerEventHandler = mergeHandlers(handlers);

			var handler = registration.Pipeline.Aggregate(
				makePretend(innerEventHandler),
				(current, handlerType) => handlerActivator.ActivateHandler<IPipelineHandler>(handlerType, current));

			return makeReal(handler);
		}
	}

	public class NoRegistrationForMessage : Exception
	{
		public NoRegistrationForMessage(Type commandType)
			: base(string.Format("No registration for message of type {0} was found", commandType.Name))
		{
		}
	}
}
