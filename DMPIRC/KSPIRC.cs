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
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KSPIRC
{
    [KSPIRCKSPAddonFixed(KSPAddon.Startup.SpaceCentre, true, typeof(KSPIRC))]
    public class KSPIRC : MonoBehaviour
    {
        internal static KSPIRC instance;

        private const string NOTICE_CHANNEL_HANDLE = "(Notice)";

        // debugging
        private const string DEBUG_CHANNEL_HANDLE = "(Debug)";
        private const bool IRC_WINDOW_HIDDEN = true;

        private delegate void IRCCommandHandler(IRCCommand cmd);
        private delegate void UserCommandHandler(UserCommand cmd);

        private Dictionary<string, IRCCommandHandler> serverCommandHandlers = new Dictionary<string, IRCCommandHandler>();
        private Dictionary<string, UserCommandHandler> userCommandHandlers = new Dictionary<string, UserCommandHandler>();

        private IRCConfig config;
        private IRCClient client;
        private IRCConfigWindow configWindow;
        private IRCChatWindow chatWindow;
        private readonly IRCLinkWindow linkWindow;
        private string version;

        private IButton windowButton;
        private ApplicationLauncherButton appLauncherButton;

        KSPIRC()
        {
            instance = this;

            GameObject.DontDestroyOnLoad(this);

            version = this.GetType().Assembly.GetName().Version.ToString();

            config = new IRCConfig();

            configWindow = new IRCConfigWindow(version, config);
            configWindow.configChangedEvent += configChanged;

            linkWindow = new IRCLinkWindow(config);
            linkWindow.hidden = true;

            chatWindow = new IRCChatWindow(linkWindow, version, config);
            chatWindow.channelClosedEvent += channelClosed;
            chatWindow.onUserCommandEntered += (e) => handleUserCommand(e.command);
            chatWindow.onShowConfigHandler += showConfig;
            chatWindow.hidden = IRC_WINDOW_HIDDEN;

            initCommandHandlers();
            initUserCommandHandlers();

            client = new IRCClient();
            client.onCommandReceived += (e) => handleServerCommand(e.command);
            client.onCommandSent += (e) => logSendCommand(e.command);
            client.onConnect += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Connecting to server " + config.host + ":" + config.port + "...");
            client.onConnected += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Server connection established.");
            client.onSSLConnected += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "SSL Server connection established.");
            client.onDisconnected += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Disconnected from server.");
            client.onConnectionFailed += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Connection failed to server.");
            client.onConnectionAttemptsExceeded += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "Connection attempts exceeded. Change config before retrying.");
            client.onSSLCertificateError += () => chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, "*", "SSL Certificate error - use this server at your own risk.");
            
            if ((config.host != null) && (config.port > 0) && (config.nick != ""))
            {
                Debug.Log("Connecting to: " + config.host + ":" + config.port);
                configWindow.hidden = true;
                client.connect(config);
            }
            else
            {
                configWindow.hidden = false;
                chatWindow.addToChannel("IRC Plugin", "*", "IRC plugin not configured, not connecting to IRC server.");
                chatWindow.addToChannel("IRC Plugin", "*", "Edit config and confirm updates to connect.");
            }

            if (ToolbarManager.ToolbarAvailable)
            {
                windowButton = ToolbarManager.Instance.add("irc", "irc");
                windowButton.TexturePath = "KSPIRC/button-regular";
                windowButton.ToolTip = "IRC";
                windowButton.Visibility = new GameScenesVisibility(GameScenes.CREDITS,
                                                                   GameScenes.EDITOR,
                                                                   GameScenes.FLIGHT,
                                                                   GameScenes.LOADING,
                                                                   GameScenes.LOADINGBUFFER,
                                                                   GameScenes.MAINMENU,
                                                                   GameScenes.PSYSTEM,
                                                                   GameScenes.SPACECENTER,
                                                                   GameScenes.TRACKSTATION);
                windowButton.OnClick += (e) => toggleChatWindow();
            }
            else
            {
                ApplicationLauncher.AppScenes scenes = ApplicationLauncher.AppScenes.ALWAYS;

                Texture toolbarButtonTexture = (Texture)GameDatabase.Instance.GetTexture("KSPIRC/button-regular", false);
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(onAppLaunchToggleOn,
                                                                                   onAppLaunchToggleOff,
                                                                                   null,
                                                                                   null,
                                                                                   null,
                                                                                   null,
                                                                                   scenes,
                                                                                   toolbarButtonTexture);
            }
        }

        private void configChanged(ConfigChangedEvent e)
        {
            throw new NotImplementedException();
        }

        void Start()
        {
            GameObject.DontDestroyOnLoad(this);
            
            var game = HighLogic.CurrentGame;

            ProtoScenarioModule psm = game.scenarios.Find(s => s.moduleName == typeof(KSPIRCScenarioModule).Name);
            if (psm == null)
            {
                psm = game.AddProtoScenarioModule(typeof(KSPIRCScenarioModule),
                    GameScenes.SPACECENTER,
                    GameScenes.TRACKSTATION,
                    GameScenes.FLIGHT,
                    GameScenes.EDITOR);
            }
            else
            {
                GameScenes[] scenes = new GameScenes[]
                {
                    GameScenes.SPACECENTER,
                    GameScenes.TRACKSTATION,
                    GameScenes.FLIGHT,
                    GameScenes.EDITOR
                };

                for (int ix = 0; ix < scenes.Length; ix++)
                {
                    GameScenes scene = scenes[ix];

                    if (!psm.targetScenes.Any(s => s == scene))
                    {
                        psm.targetScenes.Add(scene);
                    }
                }
            }
        }

        public void OnDestroy()
        {
            Debug.Log("KSPIRC::OnDestroy");

            if (windowButton != null)
            {
                windowButton.Destroy();
                windowButton = null;
            }

            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.DisableMutuallyExclusive(appLauncherButton);
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
        }

        void onAppLaunchToggleOn()
        {
            chatWindow.hidden = false;
        }

        void onAppLaunchToggleOff()
        {
            chatWindow.hidden = true;
        }

        internal void OnSave()
        {
            // not saving to persistence file, but this is a handy way to ensure
            // we keep config save up to date, particularly with window locations
            this.chatWindow.UpdateConfig();
            this.configWindow.UpdateConfig();
            this.linkWindow.UpdateConfig();
            this.config.Save();
        }

        #region gui

        public void OnGUI()
        {
            // auto-hide window
            if (windowButton != null && !windowButton.Visibility.Visible)
            {
                configWindow.hidden = true;
                chatWindow.hidden = true;
            }

            if (chatWindow.hidden && (chatWindow.anyChannelsHighlightedPrivateMessage))
            {
                updateToolbarButton("pm", true);
            }
            else
            {
                if (chatWindow.hidden && chatWindow.anyChannelsHighlightedMessage)
                {
                    updateToolbarButton("message");
                }
                else if (chatWindow.hidden && chatWindow.anyChannelsHighlightedJoin)
                {
                    updateToolbarButton("join");
                }
                else
                {
                    updateToolbarButton("regular");
                }
            }

            configWindow.draw();
            linkWindow.draw();
            chatWindow.draw();
        }

        private void updateToolbarButton(string textureName)
        {
            updateToolbarButton(textureName, false, false);
        }

        private void updateToolbarButton(string textureName, bool important)
        {
            updateToolbarButton(textureName, true, important);
        }

        private void updateToolbarButton(string textureName, bool updateImportant, bool important)
        {
            if (windowButton != null)
            {
                windowButton.TexturePath = "KSPIRC/button-" + textureName;
                if (important)
                {
                    windowButton.Important = important;
                }
            }
            if (this.appLauncherButton != null)
            {
                Texture toolbarButtonPMTexture = (Texture)GameDatabase.Instance.GetTexture("KSPIRC/button-" + textureName, false);
                appLauncherButton.SetTexture(toolbarButtonPMTexture);
            }
        }

        private void toggleChatWindow()
        {
            chatWindow.hidden = !chatWindow.hidden;
        }

        #endregion


        public void Update()
        {
            client.update();
        }

        private void logSendCommand(IRCCommand cmd)
        {
            if (config.debug)
            {
                chatWindow.addToChannel(DEBUG_CHANNEL_HANDLE, "CLIENT", cmd.ToString());
            }
        }


        private void channelClosed(ChannelEvent e)
        {
            if (e.handle.StartsWith("#"))
            {
                client.send(new IRCCommand(null, "PART", e.handle));
            }
        }

        private void showConfig(ShowConfigEvent e)
        {
            configWindow.hidden = false;
        }


        #region server commands

        private void initCommandHandlers()
        {
            IRCCommandHandler ignoreServerCommand = (cmd) => { };

            serverCommandHandlers.Add("JOIN", serverCommandJOIN);
            serverCommandHandlers.Add("KICK", serverCommandKICK);
            serverCommandHandlers.Add("MODE", serverCommandMODE);
            serverCommandHandlers.Add("NICK", serverCommandNICK);
            serverCommandHandlers.Add("NOTICE", serverCommandNOTICE);
            serverCommandHandlers.Add("PART", serverCommandPART);
            serverCommandHandlers.Add("PING", serverCommandPING);
            serverCommandHandlers.Add("PONG", ignoreServerCommand);
            serverCommandHandlers.Add("PRIVMSG", serverCommandPRIVMSG);
            serverCommandHandlers.Add("QUIT", serverCommandQUIT);
            serverCommandHandlers.Add("TOPIC", serverCommandTOPIC);
            serverCommandHandlers.Add("332", serverCommandTopic);
            serverCommandHandlers.Add("353", serverCommandNameReply);
            serverCommandHandlers.Add("366", serverCommandEndOfNames);
            serverCommandHandlers.Add("376", serverCommandEndOfMotd);
        }

        private void handleServerCommand(IRCCommand cmd)
        {
            bool unknown = !serverCommandHandlers.ContainsKey(cmd.command);

            if (config.debug)
            {
                chatWindow.addToChannel(DEBUG_CHANNEL_HANDLE, "SERVER", (unknown ? "(unknown) " : "") + cmd);
            }

            if (!unknown)
            {
                serverCommandHandlers[cmd.command](cmd);
            }
        }

        private void serverCommandEndOfMotd(IRCCommand cmd)
        {
            if (config.twitch)
            {
                client.send(new IRCCommand(null, "CAP", "REQ", "twitch.tv/membership"));
            }
        }

        private void serverCommandPING(IRCCommand cmd)
        {
            client.send(new IRCCommand(null, "PONG", cmd.parameters[0]));
        }

        private void serverCommandNOTICE(IRCCommand cmd)
        {
            chatWindow.addToChannel(NOTICE_CHANNEL_HANDLE, cmd.shortPrefix ?? "SERVER", cmd.parameters.Last());
        }

        private void serverCommandPRIVMSG(IRCCommand cmd)
        {
            string handle = cmd.parameters[0].StartsWith("#") ? cmd.parameters[0] : cmd.shortPrefix;
            if (cmd is CTCPCommand)
            {
                CTCPCommand c = (CTCPCommand)cmd;
                if (c.ctcpCommand == "ACTION")
                {
                    chatWindow.addToChannel(handle, "*", cmd.shortPrefix + " " + c.ctcpParameters);
                }
                else if ((c.ctcpCommand == "VERSION") && !handle.StartsWith("#"))
                {
                    if (c.ctcpParameters == null)
                    {
                        chatWindow.addToChannel(handle, "*", "VERSION");

                        client.send(new CTCPCommand(null, handle, "VERSION", "Internet Relay Chat Plugin " + version + " for Kerbal Space Program"));
                    }
                    else
                    {
                        chatWindow.addToChannel(handle, "*", handle + " uses client: " + c.ctcpParameters);
                    }
                }
            }
            else
            {
                chatWindow.addToChannel(handle, cmd.shortPrefix, cmd.parameters.Last());
            }
        }

        private void serverCommandNameReply(IRCCommand cmd)
        {
            string[] names = cmd.parameters.Last().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            chatWindow.addChannelNames(cmd.parameters[cmd.parameters.Length - 2], names);
        }

        private void serverCommandEndOfNames(IRCCommand cmd)
        {
            chatWindow.endOfChannelNames(cmd.parameters[cmd.parameters.Length - 2]);
        }

        private void serverCommandTopic(IRCCommand cmd)
        {
            chatWindow.setChannelTopic(cmd.parameters[cmd.parameters.Length - 2], cmd.parameters.Last());
            chatWindow.addToChannel(cmd.parameters[cmd.parameters.Length - 2], "*", "Channel topic is: " + cmd.parameters.Last());
        }

        private void serverCommandJOIN(IRCCommand cmd)
        {
            chatWindow.addToChannel(cmd.parameters[0], "*", cmd.shortPrefix + " has joined " + cmd.parameters[0], cmd);
            chatWindow.addSingleChannelName(cmd.parameters[0], cmd.shortPrefix);
        }

        private void serverCommandKICK(IRCCommand cmd)
        {
            string text = cmd.shortPrefix + " kicked " + cmd.parameters[1] + " from " + cmd.parameters[0];
            if (cmd.parameters.Length > 2)
            {
                text += " (" + cmd.parameters[2] + ")";
            }
            chatWindow.addToChannel(cmd.parameters[0], "*", text);
            chatWindow.removeChannelName(cmd.parameters[0], cmd.parameters[1]);
        }

        private void serverCommandPART(IRCCommand cmd)
        {
            if (cmd.shortPrefix != config.nick)
            {
                string text = cmd.shortPrefix + " has left " + cmd.parameters[0];
                if (cmd.parameters.Length > 1)
                {
                    text += " (" + cmd.parameters[1] + ")";
                }
                chatWindow.addToChannel(cmd.parameters[0], "*", text, cmd);
                chatWindow.removeChannelName(cmd.parameters[0], cmd.shortPrefix);
            }
        }

        private void serverCommandQUIT(IRCCommand cmd)
        {
            string text = cmd.shortPrefix + " has quit";
            if (cmd.parameters.Length > 0)
            {
                text += " (" + cmd.parameters[0] + ")";
            }

            string[] channelsContainingName = chatWindow.getChannelsContainingName(cmd.shortPrefix);
            for (int ix = 0; ix < channelsContainingName.Length; ix++)
            {
                string handle = channelsContainingName[ix];

                chatWindow.addToChannel(handle, "*", text, cmd);
                chatWindow.removeChannelName(handle, cmd.shortPrefix);
            }
        }

        private void serverCommandNICK(IRCCommand cmd)
        {
            string oldName = cmd.shortPrefix;
            string newName = cmd.parameters.Last();

            string[] channelsContainingName = chatWindow.getChannelsContainingName(oldName);
            for (int ix = 0; ix < channelsContainingName.Length; ix++)
            {
                string handle = channelsContainingName[ix];
                chatWindow.renameInChannel(handle, oldName, newName);
                chatWindow.addToChannel(handle, "*", oldName + " is now known as " + newName);
            }
        }

        private void serverCommandMODE(IRCCommand cmd)
        {
            // channel mode
            if (cmd.parameters[0].StartsWith("#"))
            {
                string channel = cmd.parameters[0];
                string mode = cmd.parameters[1];
                if ((mode == "+o") || (mode == "-o") || (mode == "+v") || (mode == "-v"))
                {
                    string name = cmd.parameters[2];
                    chatWindow.changeUserModeInChannel(channel, name, mode);
                    chatWindow.addToChannel(channel, "*", cmd.shortPrefix + " sets mode " + mode + " on " + name);
                }
            }
        }

        private void serverCommandTOPIC(IRCCommand cmd)
        {
            string topic = null;
            if (cmd.parameters.Length > 1)
            {
                topic = cmd.parameters.Last();
            }
            chatWindow.setChannelTopic(cmd.parameters[0], topic);
            chatWindow.addToChannel(cmd.parameters[0], "*", cmd.shortPrefix + " sets channel topic to: " + (topic ?? ""));
        }


        #endregion


        #region user commands

        private void initUserCommandHandlers()
        {
            userCommandHandlers.Add("DEOP", userCommandDEOP);
            userCommandHandlers.Add("DEVOICE", userCommandDEVOICE);
            userCommandHandlers.Add("J", userCommandJ);
            userCommandHandlers.Add("KICK", userCommandKICK);
            userCommandHandlers.Add("ME", userCommandME);
            userCommandHandlers.Add("MSG", userCommandMSG);
            userCommandHandlers.Add("OP", userCommandOP);
            userCommandHandlers.Add("TOPIC", userCommandTOPIC);
            userCommandHandlers.Add("VOICE", userCommandVOICE);
        }

        private void handleUserCommand(UserCommand cmd)
        {
            if (userCommandHandlers.ContainsKey(cmd.command))
            {
                userCommandHandlers[cmd.command](cmd);
            }
            else
            {
                client.send(cmd.command + " " + cmd.parameters);
            }
        }

        private void userCommandME(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if (cmd.parameters.Length > 0)
            {
                client.send(new CTCPCommand(null, handle, "ACTION", cmd.parameters));
                chatWindow.addToChannel(handle, "*", config.nick + " " + cmd.parameters);
            }
        }

        private void userCommandMSG(UserCommand cmd)
        {
            string[] parts = cmd.parameters.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                client.send(new IRCCommand(null, "PRIVMSG", parts[0], parts[1]));
                chatWindow.addToChannel(parts[0], config.nick, parts[1]);
            }
        }

        private void userCommandJ(UserCommand cmd)
        {
            handleUserCommand(new UserCommand("JOIN", cmd.parameters));
        }

        private void userCommandTOPIC(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#"))
            {
                client.send(new IRCCommand(null, "TOPIC", handle, cmd.parameters));
            }
        }

        private void userCommandOP(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#"))
            {
                sendUserChannelMode(handle, cmd.parameters, "+o");
            }
        }

        private void userCommandDEOP(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#"))
            {
                sendUserChannelMode(handle, cmd.parameters, "-o");
            }
        }

        private void userCommandVOICE(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#"))
            {
                sendUserChannelMode(handle, cmd.parameters, "+v");
            }
        }

        private void userCommandDEVOICE(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            if ((cmd.parameters.Length > 0) && (handle != null) && handle.StartsWith("#"))
            {
                sendUserChannelMode(handle, cmd.parameters, "-v");
            }
        }

        private void sendUserChannelMode(string handle, string name, string mode)
        {
            client.send(new IRCCommand(null, "MODE", handle, mode, name));
        }

        private void userCommandKICK(UserCommand cmd)
        {
            string handle = chatWindow.getCurrentChannelName();
            string[] parts = cmd.parameters.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                if (parts.Length == 1)
                {
                    client.send(new IRCCommand(null, "KICK", handle, parts[0]));
                }
                else
                {
                    client.send(new IRCCommand(null, "KICK", handle, parts[0], parts.Last()));
                }
            }
        }

        #endregion
    }
    
    class KSPIRCScenarioModule : ScenarioModule
    {
        internal void OnSave(ConfigNode gameNode)
        {
            if (KSPIRC.instance != null)
            {
                KSPIRC.instance.OnSave();
            }
        }
    }
}