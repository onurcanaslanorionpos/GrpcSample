using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcChatSample.Common;
using GrpcChatSample2.Server.Infrastructure;
using GrpcChatSample2.Server.Model;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace GrpcChatSample2.Server.Rpc
{
    [Export]
    public class ChatGrpcService : Chat.ChatBase
    {
        [Import]
        private Logger m_logger = null;

        [Import]
        private ChatService m_chatService = null;
        private readonly Empty m_empty = new Empty();

        public override async Task Subscribe(Empty request, IServerStreamWriter<ChatLog> responseStream, ServerCallContext context)
        {
            var peer = context.Peer; // keep peer information because it is not available after disconnection
            m_logger.Info($"{peer} subscribes.");

            context.CancellationToken.Register(() => m_logger.Info($"{peer} cancels subscription."));

            // Completing the method means disconnecting the stream by server side.
            // If subscribing IObservable, you have to block this method after the subscription.
            // I prefer converting IObservable to IAsyncEnumerable to consume the sequense here
            // because gRPC interface is in IAsyncEnumerable world.
            // Note that the chat service model itself is in IObservable world
            // because chat is naturally recognized as an event sequence.

            try
            {
                await m_chatService.GetChatLogsAsObservable()
                    .ToAsyncEnumerable()
                    .ForEachAwaitAsync(async (x) => await responseStream.WriteAsync(x), context.CancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                m_logger.Info($"{peer} unsubscribed.");
            }
        }

        public override Task<ChatLogT> Write(ChatLog request, ServerCallContext context)
        {
            m_logger.Info($"{context.Peer} {request}");

            m_chatService.Add(request);

            var t = new ChatLogT { Name = "onur" };

            return Task.FromResult(t);
        }

        public override Task<LoginResult> Login(LoginObject request, ServerCallContext context)
        {
            var lr = new LoginResult {
                Error = string.Empty,
                IsAuth   = false,
            };

            if (request.Password == "123456" && request.Name == "can")
                lr.IsAuth = true;

            return Task.FromResult(lr);
        }
    }
}
