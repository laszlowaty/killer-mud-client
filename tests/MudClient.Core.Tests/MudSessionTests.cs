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

    [Theory]
    [InlineData(MudTextEncodings.Windows1250)]
    [InlineData(MudTextEncodings.Iso88592)]
    public async Task EncodingMode_ExplicitNonUtf8_RoundTripsPolishDiacritics(string encodingName)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var session = new MudSession
            {
                EncodingMode = encodingName,
            };

            var accept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var serverConnection = await accept;
            var serverStream = serverConnection.GetStream();

            // Drain the initial telnet option negotiation the client sends on connect
            // before the command bytes we actually care about.
            var negotiationBuffer = new byte[256];
            await Task.Delay(50, timeout.Token);
            while (serverConnection.Available > 0)
            {
                await serverStream.ReadAsync(negotiationBuffer.AsMemory(0, serverConnection.Available), timeout.Token);
            }

            await session.SendCommandAsync("zażółć gęślą jaźń", timeout.Token);

            var buffer = new byte[256];
            var read = await serverStream.ReadAsync(buffer.AsMemory(), timeout.Token);
            var received = MudTextEncodings.Resolve(encodingName).GetString(buffer, 0, read).TrimEnd('\r', '\n');
            Assert.Equal("zażółć gęślą jaźń", received);

            var textReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.TextReceived += text => textReceived.TrySetResult(text);

            var reply = MudTextEncodings.Resolve(encodingName).GetBytes("Łąka śpi.\r\n");
            await serverStream.WriteAsync(reply, timeout.Token);

            Assert.Equal("Łąka śpi.\r\n", await textReceived.Task.WaitAsync(timeout.Token));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task EncodingMode_Auto_DetectsWindows1250FromServerBytes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var session = new MudSession(); // EncodingMode defaults to Auto.

            var accept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var serverConnection = await accept;
            var serverStream = serverConnection.GetStream();

            // Drain the initial telnet option negotiation the client sends on connect
            // before the command bytes we actually care about.
            var negotiationBuffer = new byte[256];
            await Task.Delay(50, timeout.Token);
            while (serverConnection.Available > 0)
            {
                await serverStream.ReadAsync(negotiationBuffer.AsMemory(0, serverConnection.Available), timeout.Token);
            }

            var textReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.TextReceived += text => textReceived.TrySetResult(text);

            var cp1250Bytes = MudTextEncodings.Resolve(MudTextEncodings.Windows1250).GetBytes("Witaj, żółwiu!\r\n");
            await serverStream.WriteAsync(cp1250Bytes, timeout.Token);

            Assert.Equal("Witaj, żółwiu!\r\n", await textReceived.Task.WaitAsync(timeout.Token));

            // Detection should have locked onto Windows-1250 for outgoing commands too.
            await session.SendCommandAsync("zażółć", timeout.Token);

            var buffer = new byte[256];
            var read = await serverStream.ReadAsync(buffer.AsMemory(), timeout.Token);
            var received = MudTextEncodings.Resolve(MudTextEncodings.Windows1250)
                .GetString(buffer, 0, read)
                .TrimEnd('\r', '\n');
            Assert.Equal("zażółć", received);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task EncodingMode_Auto_KeepsUtf8WhenServerSendsValidUtf8()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            await using var session = new MudSession();

            var accept = listener.AcceptTcpClientAsync(timeout.Token);
            await session.ConnectAsync(IPAddress.Loopback.ToString(), endpoint.Port, timeout.Token);
            using var serverConnection = await accept;
            var serverStream = serverConnection.GetStream();

            var textReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.TextReceived += text => textReceived.TrySetResult(text);

            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes("Witaj, żółwiu!\r\n");
            await serverStream.WriteAsync(utf8Bytes, timeout.Token);

            Assert.Equal("Witaj, żółwiu!\r\n", await textReceived.Task.WaitAsync(timeout.Token));
        }
        finally
        {
            listener.Stop();
        }
    }
}
