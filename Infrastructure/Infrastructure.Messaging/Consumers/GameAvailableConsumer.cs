using CrossCutting.Messaging.Events;
using MassTransit;

namespace Infrastructure.Messaging.Consumers
{
    public class GameAvailableConsumer : IConsumer<GameAvailableEvent>
    {
        public async Task Consume(ConsumeContext<GameAvailableEvent> context)
        {
            var evento = context.Message;
            // TO DO: adicionar logica a ser executada com o evento consumido
            await Task.CompletedTask;
        }
    }
}
