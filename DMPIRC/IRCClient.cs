/*
KSPIRC - Internet Relay Chat plugin for Kerbal Space Program.
Copyright (C) 2013 Maik Schreiber

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using UnityEngine;

namespace KSPIRC
{
    class IRCClient
    {
        private const long SERVER_PING_INTERVAL = 30000;
        private const long AUTO_JOIN_DELAY = 5000;
        private const long AUTO_JOIN_TIME_BETWEEN_ATTEMPTS = 30000;
        private const int MAX_CONNECT_RETRIES = 5;

        public event IRCCommandHandler onCommandReceived;
        public event IRCCommandHandler onCommandSent;
        public event Callback onConnect;
        public event Callback onConnected;
        public event Callback onDisconnected;
        public event Callback onConnectionFailed;
        public event Callback onConnectionAttemptsExceeded;
        public event Callback onSSLConnected;
        public event Callback onSSLCertificateError;

        private IRCConfig config;

        private TcpClient client;
        //private NetworkStream stream;
        private Stream stream;
        private byte[] buffer = new byte[10240];
        private StringBuilder textBuffer = new StringBuilder();
        private bool tryReconnect = true;
        private bool connected;
        private long connectTime;
        private long lastAutoJoinsSentTime = -1;
        private bool autoJoinsSent = true;
        private long lastServerPing = DateTime.UtcNow.Ticks / 10000;
        private int connectionAttempts = 0;

        public void connect(IRCConfig config)
        {
            this.connectionAttempts = 0;
            this.config = config;
            connect();
        }

        public bool PermissiveCertificateValidationCallback(object sender,
                                                     X509Certificate certificate,
                                                     X509Chain chain,
                                                     SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                Debug.Log("Certificate all good");
                return true;
            }

            Debug.LogError("Certificate error: [" + sslPolicyErrors + "]");

            // Do not allow this client to communicate with unauthenticated servers. 
            // meh actually yes
            if (onSSLCertificateError != null)
            {
                onSSLCertificateError();
            }
            return true;
        }

        private void connect()
        {
            doDisconnect();

            if (onConnect != null)
            {
                onConnect();
            }

            autoJoinsSent = false;

            if (connectionAttempts++ > MAX_CONNECT_RETRIES)
            {
                Debug.LogWarning("Too many failed attempts to connect to " + (config.twitch ? "Twitch" : "host[" + config.host + "] port[" + config.port + "]"));
                if (onConnectionAttemptsExceeded != null)
                {
                    onConnectionAttemptsExceeded();
                }
                return;
            }

            try
            {
                //System.Security.Cryptography.Aes aesCrypto = new System.Security.Cryptography.AesCryptoServiceProvider();

                client = new TcpClient();
                if (config.twitch)
                {
                    client.Connect("irc.chat.twitch.tv", 443);
                }
                else
                {
                    client.Connect(config.host, config.port);
                }
                stream = client.GetStream();
                if (config.secure || config.twitch)
                {
                    SslStream sslStream = new SslStream(stream, 
                                                        false, 
                                                        new RemoteCertificateValidationCallback (PermissiveCertificateValidationCallback), null);
                    sslStream.AuthenticateAsClient(config.host);
                    if (onSSLConnected != null)
                    {
                        onSSLConnected();
                    }
                    stream = sslStream;
                }

                if ((config.serverPassword != null) && (config.serverPassword != ""))
                {
                    send(new IRCCommand(null, "PASS", config.serverPassword));
                }
                send(new IRCCommand(null, "NICK", config.nick));
                send(new IRCCommand(null, "USER", (String.IsNullOrEmpty(config.user) ? config.nick : config.user), "8", "*", config.nick));


                connectTime = DateTime.UtcNow.Ticks / 10000;
                connected = true;

                if (onConnected != null)
                {
                    onConnected();
                }
            }
            catch (Exception ex)
            {
                handleException(ex, true);
            }
        }

        public void disconnect()
        {
            tryReconnect = false;
            doDisconnect();
        }

        private void doDisconnect()
        {
            bool wasConnected = connected;

            if (stream != null)
            {
                try
                {
                    send(new IRCCommand(null, "QUIT", "Build. Fly. Dream."));
                }
                catch
                {
                    // ignore
                }
            }

            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }

            connected = false;
            textBuffer.Clear();

            if (wasConnected && (onDisconnected != null))
            {
                onDisconnected();
            }
        }

        private void reconnect()
        {
            if (tryReconnect && connected)
            {
                try
                {
                    tryReconnect = false;
                    doDisconnect();
                    connect();
                }
                finally
                {
                    tryReconnect = true;
                }
            }
        }

        public void update()
        {
            if (connected)
            {
                try
                {
                    if (stream.CanRead)
                    {
                        //while (stream.DataAvailable)
                        while (client.Available > 0)
                        {
                            int numBytes = stream.Read(buffer, 0, buffer.Length);
                            if (numBytes > 0)
                            {
                                string text = Encoding.UTF8.GetString(buffer, 0, numBytes);
                                textBuffer.Append(text);
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    handleException(ex, true);
                }

                if (textBuffer.Length > 0)
                {
                    for (; ; )
                    {
                        int pos = textBuffer.ToString().IndexOf("\r\n");
                        if (pos >= 0)
                        {
                            string line = textBuffer.ToString().Substring(0, pos);
                            textBuffer.Remove(0, pos + 2);

                            if (onCommandReceived != null)
                            {
                                try
                                {
                                    IRCCommand cmd = IRCCommand.fromLine(line);
                                    onCommandReceived(new IRCCommandEvent(cmd));
                                }
                                catch (ArgumentException e)
                                {
                                    Debug.LogException(e);
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // send something to socket to potentially trigger SocketException elsewhere when reading
                // off the socket
                long now = DateTime.UtcNow.Ticks / 10000L;
                if ((now - lastServerPing) >= SERVER_PING_INTERVAL)
                {
                    lastServerPing = now;
                    send("PING :" + now);
                }

                // only send auto joins if we've been connected for AUTO_JOIN_DELAY millis to allow time for post-connection stuff (user, nick)
                // and if AUTO_JOIN_TIME_BETWEEN_ATTEMPTS has elapsed since the last auto join happened, to avoid other join spam
                if (!autoJoinsSent && 
                    ((now - lastAutoJoinsSentTime) >= AUTO_JOIN_TIME_BETWEEN_ATTEMPTS ) && 
                    ((now - connectTime) >= AUTO_JOIN_DELAY))
                {
                    autoJoinChannels();
                }
            }
        }

        public void send(IRCCommand cmd)
        {
            if (onCommandSent != null)
            {
                onCommandSent(new IRCCommandEvent(cmd));
            }
            byte[] data = Encoding.UTF8.GetBytes(cmd.ToString() + "\r\n");
            try
            {
                stream.Write(data, 0, data.Length);
            }
            catch (SocketException ex)
            {
                handleException(ex, true);
            }
            catch (IOException ex)
            {
                handleException(ex, true);
            }
        }

        public void send(string cmdAndParams)
        {
            if (onCommandSent != null)
            {
                onCommandSent(new IRCCommandEvent(IRCCommand.fromLine(cmdAndParams)));
            }
            byte[] data = Encoding.UTF8.GetBytes(cmdAndParams + "\r\n");
            try
            {
                stream.Write(data, 0, data.Length);
            }
            catch (SocketException ex)
            {
                handleException(ex, true);
            }
            catch (IOException ex)
            {
                handleException(ex, true);
            }
        }

        private void handleException(Exception ex, bool shouldReconnect)
        {
            Debug.LogException(ex);
            if (onConnectionFailed != null)
            {
                onConnectionFailed();
            }

            if (shouldReconnect)
            {
                reconnect();
            }
        }

        private void autoJoinChannels()
        {
            string[] autoJoinChannels = config.channels.Split(' ');
            for (int ix = 0; ix < autoJoinChannels.Length; ix++)
            {
                string channel = autoJoinChannels[ix];
                if (channel.StartsWith("#"))
                {
                    send("JOIN " + channel);
                }
            }
            lastAutoJoinsSentTime = DateTime.UtcNow.Ticks / 10000L;
            autoJoinsSent = true;
        }
    }
}