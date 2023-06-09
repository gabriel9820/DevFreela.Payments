using DevFreela.Payments.API.IntegrationEvents;
using DevFreela.Payments.API.Models;
using DevFreela.Payments.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DevFreela.Payments.API.Consumers
{
    public class ProcessPaymentConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider _serviceProvider;

        private const string PAYMENTS_QUEUE = "Payments";
        private const string APPROVED_PAYMENTS_QUEUE = "ApprovedPayments";

        public ProcessPaymentConsumer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            var factory = new ConnectionFactory
            {
                HostName = "localhost",
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: PAYMENTS_QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueDeclare(
               queue: APPROVED_PAYMENTS_QUEUE,
               durable: false,
               exclusive: false,
               autoDelete: false,
               arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (sender, eventArgs) =>
            {
                var payloadBytes = eventArgs.Body.ToArray();
                var payloadString = Encoding.UTF8.GetString(payloadBytes);
                var paymentInfo = JsonSerializer.Deserialize<PaymentInfoInputModel>(payloadString);

                var paymentResult = await ProcessPayment(paymentInfo);

                if (paymentResult)
                {
                    var approvedPayment = new ApprovedPaymentIntegrationEvent(paymentInfo.ProjectId);
                    var approvedPaymentString = JsonSerializer.Serialize(approvedPayment);
                    var approvedPaymentBytes = Encoding.UTF8.GetBytes(approvedPaymentString);

                    _channel.BasicPublish(
                        exchange: "",
                        routingKey: APPROVED_PAYMENTS_QUEUE,
                        basicProperties: null,
                        body: approvedPaymentBytes);
                }

                _channel.BasicAck(eventArgs.DeliveryTag, false);
            };

            _channel.BasicConsume(PAYMENTS_QUEUE, false, consumer);

            return Task.CompletedTask;
        }

        private async Task<bool> ProcessPayment(PaymentInfoInputModel paymentInfo)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                return await paymentService.Process(paymentInfo);
            }
        }
    }
}
