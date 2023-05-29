using RabbitMQ.Client;

namespace TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Rabbit;

public interface IRabbitPublisher
{
    void Initialize(IModel channel);
    void Publish(string exchange, object data);
}
