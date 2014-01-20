using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Mono.Cecil;
using UnityEngine;
using ScrollsModLoader.Interfaces;

namespace GameReplay.Mod
{
	public class Player : ICommListener, IBattleModeUICallback
	{

		Thread replay = null;
		private volatile bool playing = false;
		private volatile bool readNextMsg = true;
		private volatile bool paused = false;

		//private String saveFolder;
		private String fileName;

		private BattleModeUI battleModeUI;
		private GameObject endGameButton;
		private Texture2D pauseButton;
		private Texture2D playButton;
        private MethodInfo dispatchMessages;

		public Player(String saveFolder)
		{
			App.Communicator.addListener(this);
			playButton = new Texture2D(83, 131);
			playButton.LoadImage(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("GameReplay.Mod.Play.png").ReadToEnd());
			pauseButton = new Texture2D(83, 131);
			pauseButton.LoadImage(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("GameReplay.Mod.Pause.png").ReadToEnd());
			//this.saveFolder = saveFolder;
            dispatchMessages = typeof(MiniCommunicator).GetMethod("_dispatchMessageToListeners", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public static MethodDefinition[] GetPlayerHooks(TypeDefinitionCollection scrollsTypes, int version)
		{
            return new MethodDefinition[] { 
                                            //scrollsTypes["MiniCommunicator"].Methods.GetMethod("_handleMessage")[0],
											scrollsTypes["GUIBattleModeMenu"].Methods.GetMethod("toggleMenu")[0],
											scrollsTypes["BattleMode"].Methods.GetMethod("OnGUI")[0],
											//scrollsTypes["BattleMode"].Methods.GetMethod("runEffect")[0],
											scrollsTypes["BattleModeUI"].Methods.GetMethod("Start")[0],
											scrollsTypes["BattleModeUI"].Methods.GetMethod("Init")[0],
											scrollsTypes["BattleModeUI"].Methods.GetMethod("Raycast")[0],
											scrollsTypes["BattleModeUI"].Methods.GetMethod("ShowEndTurn")[0],
										   };
		}

		public bool WantsToReplace (InvocationInfo info)
		{
            
			if (!playing)
				return false;

			switch ((String)info.targetMethod)
			{
				case "runEffect":
				{
					return paused;
				}
                case "_handleMessage":
				{
					return paused | !readNextMsg;
				}
				case "ShowEndTurn":
				{
					return true;
				}
			}
			return false;
		}

		public void ReplaceMethod (InvocationInfo info, out object returnValue) {
            returnValue = null;
			switch ((String)info.targetMethod) {
				case "ShowEndTurn":
				case "runEffect":
					returnValue = null;
					return;
                case "_handleMessage":
					returnValue = true;
					return;
			}
		}

		public void BeforeInvoke(InvocationInfo info)
		{

			switch ((String)info.targetMethod)
			{
				case "OnGUI":
					{
						if (playing) {
							typeof(BattleMode).GetMethod ("deselectAllTiles", BindingFlags.Instance | BindingFlags.NonPublic).Invoke (info.target, null);
						}
					}
					break;
                case "_handleMessage":
					{
						if (playing && readNextMsg != false)
						{
							readNextMsg = false;
						}
					}
					break;
				case "toggleMenu":
					{
						if (playing)
						{ //quit on Esc/Back Arrow
							playing = false;
							App.Communicator.setData("");
							SceneLoader.loadScene("_Lobby");
						}
					}
					break;
			}
		}

		public void handleMessage(Message msg)
		{
			if (playing && msg is NewEffectsMessage && msg.getRawText().Contains("EndGame"))
			{
				playing = false;
				//App.Communicator.setData("");
			}
		}
		public void onConnect(OnConnectData ocd)
		{
			return;
		}

		public void LaunchReplay(String name)
		{
			if (name == null || name.Equals (""))
				return;

			fileName = name;
			replay = new Thread(new ThreadStart(LaunchReplay));
			replay.Start();
		}


		private void LaunchReplay()
		{
			playing = true;

			String log = File.ReadAllText (fileName).Split(new char[] {'}'}, 2)[1];
			//FIX Profile ID
			JsonMessageSplitter jsonms = new JsonMessageSplitter();
			jsonms.feed(log);
			jsonms.runParsing();
			String line = jsonms.getNextMessage();
			String idWhite = null;
			String idBlack = null;
			String realID = null;
            /*Console.WriteLine("FIX ID "+line);
			while (line != null) {
				try {
                    Message msg = MessageFactory.create(MessageFactory.getMessageName(line), line); 
					if (msg is GameInfoMessage) {
						idWhite = (msg as GameInfoMessage).getPlayerProfileId (TileColor.white);
						idBlack = (msg as GameInfoMessage).getPlayerProfileId (TileColor.black);
					}
					if (msg is NewEffectsMessage && realID== null) {
                        string strg = "\"HandUpdate\":{\"profileId\":\"";
						if (msg.getRawText ().Contains (strg+idWhite)) {
							realID = idWhite;
                            Console.WriteLine("realid is white");
						}
						if (msg.getRawText ().Contains (strg+idBlack)) {
							realID = idBlack;
                            Console.WriteLine("realid is black");
						}
					}
					if (msg is PingMessage) {
						log.Replace(msg.getRawText(), "");
					}
				} catch {
				}
				jsonms.runParsing();
				line = jsonms.getNextMessage();
			}
            */
			if (realID != null) {
                Console.WriteLine("replace" + realID + "with" + App.MyProfile.ProfileInfo.id);
				log = log.Replace (realID, App.MyProfile.ProfileInfo.id);
			}
            App.SceneValues.battleMode = new SceneValues.SV_BattleMode(GameMode.Replay);//GameMode.Play
			SceneLoader.loadScene("_BattleModeView");
            //App.SceneValues.battleMode.gameMode = GameMode.Replay;
			//App.Communicator.setData(log);//doesnt seem to work anymore ( game stops to work )
            jsonms.clear();
            jsonms.feed(log);
            jsonms.runParsing();
            Console.WriteLine("Playing:\r\n" + log);
            line = jsonms.getNextMessage();
			while (playing)
			{
                App.Communicator.setEnabled(true, true);
                Console.WriteLine("nxt replay mssg: "+line);
                Message msg = MessageFactory.create(MessageFactory.getMessageName(line), line);

                if (msg is CardInfoMessage) // CardInfoMessages are not very informative for players :D
                {
                    jsonms.runParsing();
                    line = jsonms.getNextMessage(); 
                    continue;
                }
               
                

                dispatchMessages.Invoke(App.Communicator, new object[] { msg });
                if (line.Contains("EndGame") && !(msg is GameChatMessageMessage) && !(msg is RoomChatMessageMessage))
                {
                    playing = false;

                }
                else
                {
                        jsonms.runParsing();
                        line = jsonms.getNextMessage();
                }
                readNextMsg = false;
                if (msg is GameChatMessageMessage || msg is PingMessage ) readNextMsg = true;
				if (readNextMsg == false)
				{
					//delay messages otherwise the game rushes through in about a minute.
					Thread.Sleep(2000);
					while (paused)
					{
						Thread.Sleep(1000);
					}
					//readNextMsg = true;
				}
			}
            Console.WriteLine("player stoped");

		}

		public void AfterInvoke(InvocationInfo info, ref object returnValue)
		{

			switch (info.targetMethod)
			{
				case "Start":
					{
						if (playing)
						{
							battleModeUI = (BattleModeUI)info.target;
							endGameButton = ((GameObject)typeof(BattleModeUI).GetField("endTurnButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(info.target));
							endGameButton.renderer.material.mainTexture = pauseButton;
							battleModeUI.StartCoroutine("FadeInEndTurn");
						}
					}
					break;
				case "Init":
					{
						if (playing)
						{
							typeof(BattleModeUI).GetField("callback", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(info.target, this);
							// NOTE: Not yet working, needs alternative ICommListener for Chat messages
							/*App.ChatUI.SetEnabled(true);
							App.ChatUI.SetLocked(false);
							App.ChatUI.Show(false);
							App.ChatUI.SetCanOpenContextMenu(false);*/
							//activate chat on replays but disable profile or trading menus (wired bugs)
						}
					}
					break;
				case "Raycast":
					{
						if (playing && endGameButton.renderer.material.mainTexture != pauseButton && endGameButton.renderer.material.mainTexture != playButton)
						{
							if (paused)
							{
								endGameButton.renderer.material.mainTexture = playButton;
							}
							else
							{
								endGameButton.renderer.material.mainTexture = pauseButton;
							}
						}
					}
					break;
			}
		}

		public bool allowEndTurn()
		{
			return true;
		}

		public void endturnPressed()
		{
			if (!playing) return;

			paused = !paused;
			if (paused)
			{
				endGameButton.renderer.material.mainTexture = playButton;
			}
			else
			{
				endGameButton.renderer.material.mainTexture = pauseButton;
			}
		}
	}
}

