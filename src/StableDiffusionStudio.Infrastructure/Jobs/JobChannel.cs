using System.Threading.Channels;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public sealed class JobChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelWriter<Guid> Writer => _channel.Writer;
    public ChannelReader<Guid> Reader => _channel.Reader;
}
