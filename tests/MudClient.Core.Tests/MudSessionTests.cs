using System.Net;
using System.Net.Sockets;
using MudClient.Core.Networking;

namespace MudClient.Core.Tests;

public sealed class MudSessionTests
{
    [Fact]
    public async Task SendCommandAsync_AfterSuccessfulWrite_RaisesCommandSent()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var session = new MudSession();
            var sent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.CommandSent += command => sent.TrySetResult(command);

            var accept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var serverConnection = await accept;

            await session.SendCommandAsync("spojrz", timeout.Token);

            Assert.Equal("spojrz", await sent.Task.WaitAsync(timeout.Token));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_AfterRemoteDisconnect_CanConnectAgain()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var session = new MudSession();
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            session.ConnectionClosed += () => closed.TrySetResult();

            var firstAccept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using (var firstServerConnection = await firstAccept)
            {
                firstServerConnection.Client.Shutdown(SocketShutdown.Both);
            }

            await closed.Task.WaitAsync(timeout.Token);
            Assert.False(session.IsConnected);

            var secondAccept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var secondServerConnection = await secondAccept;

            Assert.True(session.IsConnected);
        }
        finally
        {
            listener.Stop();
        }
    }
}
