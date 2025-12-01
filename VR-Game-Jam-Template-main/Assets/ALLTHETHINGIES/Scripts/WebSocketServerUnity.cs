// WebSocketServerUnity.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketServerUnity : MonoBehaviour
{
    [Tooltip("TCP port that mobile devices should connect to with ws://<PC_IP>:<port>/orient/")]
    public int port = 8080;

    private HttpListener httpListener;
    private CancellationTokenSource cancellationTokenSource;
    private readonly List<WebSocket> activeSockets = new();
    private readonly object socketLock = new();

    // A static event that other scripts can subscribe to
    public static event Action<string> OnMessageReceived;

    void Start()
    {
        StartServer();
    }

    void OnDestroy()
    {
        StopServer();
    }

    public void StartServer()
    {
        if (httpListener != null)
        {
            Debug.LogWarning("WebSocket server is already running");
            return;
        }

        try
        {
            cancellationTokenSource = new CancellationTokenSource();
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://*:{port}/orient/");
            httpListener.Start();

            _ = Task.Run(() => AcceptLoopAsync(cancellationTokenSource.Token));

            Debug.Log($"WebSocket server started on ws://{GetLocalIPAddress()}:{port}/orient/");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start WebSocket server: " + ex);
            StopServer();
        }
    }

    public void StopServer()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;

        if (httpListener != null)
        {
            try
            {
                httpListener.Stop();
                httpListener.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Error while stopping WebSocket server: " + ex.Message);
            }
            httpListener = null;
        }

        lock (socketLock)
        {
            foreach (var socket in activeSockets)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait(100);
                    }
                }
                catch { }
                finally
                {
                    socket.Dispose();
                }
            }
            activeSockets.Clear();
        }

        Debug.Log("WebSocket server stopped");
    }

    async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && httpListener != null)
        {
            HttpListenerContext context = null;

            try
            {
                context = await httpListener.GetContextAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            if (context == null)
                continue;

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            _ = HandleConnectionAsync(context, token);
        }
    }

    async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken token)
    {
        WebSocket socket = null;

        try
        {
            var websocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            socket = websocketContext.WebSocket;

            lock (socketLock)
            {
                activeSockets.Add(socket);
            }

            await ReceiveLoopAsync(socket, token);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("WebSocket connection error: " + ex.Message);
        }
        finally
        {
            if (socket != null)
            {
                lock (socketLock)
                {
                    activeSockets.Remove(socket);
                }

                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                }
                catch { }
                finally
                {
                    socket.Dispose();
                }
            }
        }
    }

    async Task ReceiveLoopAsync(WebSocket socket, CancellationToken token)
    {
        var buffer = new byte[4096];

        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var memoryStream = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    return;
                }

                memoryStream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(memoryStream.ToArray());
                OnMessageReceived?.Invoke(message);
            }
        }
    }

    // small helper to get local IP for instructions
    string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }
}
