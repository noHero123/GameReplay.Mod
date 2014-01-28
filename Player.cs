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
	public class Player : ICommListener, IOkStringCancelCallback, IOkCancelCallback
	{
        private BattleMode bm = null;
        private List<string> logList = new List<string>();
        private Dictionary<int, int> turnToLogLine = new Dictionary<int, int>();
        private int minTurn, maxTurn;

		Thread replay = null;
        public volatile bool playing = false;
		private volatile bool readNextMsg = true;
        private volatile bool paused = false;

		//private String saveFolder;
		private String fileName;

		private BattleModeUI battleModeUI;
		private GameObject endGameButton;
		private Texture2D pauseButton;
		private Texture2D playButton;
        private MethodInfo dispatchMessages;

        private GUIStyle buttonStyle;
        private int seekTurn = 0;
        private bool readedGameState=false;

        public void setbm(BattleMode b) { this.bm = b; }
        private FieldInfo effectsField=  typeof(BattleMode).GetField("effects", BindingFlags.NonPublic | BindingFlags.Instance);


		public Player(String saveFolder)
		{
			App.Communicator.addListener(this);
			playButton = new Texture2D(83, 131);
			playButton.LoadImage(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("GameReplay.Mod.Play.png").ReadToEnd());
			pauseButton = new Texture2D(83, 131);
			pauseButton.LoadImage(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("GameReplay.Mod.Pause.png").ReadToEnd());
			//this.saveFolder = saveFolder;
            dispatchMessages = typeof(MiniCommunicator).GetMethod("_dispatchMessageToListeners", BindingFlags.NonPublic | BindingFlags.Instance);




            GUISkin skin = (GUISkin)Resources.Load("_GUISkins/LobbyMenu");
            this.buttonStyle = skin.button;
            this.buttonStyle.normal.background = this.buttonStyle.hover.background;
            this.buttonStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
            this.buttonStyle.fontSize = (int)((10 + Screen.height / 72) * 0.65f);

            this.buttonStyle.hover.textColor = new Color(0.80f, 0.80f, 0.80f, 1f);

            this.buttonStyle.active.background = this.buttonStyle.hover.background;
            this.buttonStyle.active.textColor = new Color(0.60f, 0.60f, 0.60f, 1f);
        
        
        
        }

		public static MethodDefinition[] GetPlayerHooks(TypeDefinitionCollection scrollsTypes, int version)
		{
            return new MethodDefinition[] { 
                                            //scrollsTypes["MiniCommunicator"].Methods.GetMethod("_handleMessage")[0],
											scrollsTypes["GUIBattleModeMenu"].Methods.GetMethod("toggleMenu")[0],
											scrollsTypes["BattleMode"].Methods.GetMethod("OnGUI")[0],
											//scrollsTypes["BattleMode"].Methods.GetMethod("runEffect")[0],
											//scrollsTypes["BattleModeUI"].Methods.GetMethod("Start")[0],
											//scrollsTypes["BattleModeUI"].Methods.GetMethod("Init")[0],
											//scrollsTypes["BattleModeUI"].Methods.GetMethod("Raycast")[0],
											scrollsTypes["BattleModeUI"].Methods.GetMethod("ShowEndTurn")[0],
										   };
		}

		public bool WantsToReplace (InvocationInfo info)
		{
            
			if (!playing)
				return false;

			switch ((String)info.targetMethod)
			{
				/*case "runEffect":
				{
					return paused;
				}
                case "_handleMessage":
				{
					return paused | !readNextMsg;
				}*/
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
                /*case "OnGUI":
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
                 */
                case "toggleMenu":
					{
						if (playing)
						{ //quit on Esc/Back Arrow
							playing = false;
							App.Communicator.setData("");
							SceneLoader.loadScene("_Lobby");
                            this.replay.Abort();
                            this.replay = null;
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
            
            // save log to list:
            this.logList.Clear();
            this.turnToLogLine.Clear();
            this.minTurn = 100000000;
            this.maxTurn = 0;
            
            while (line != null)
            {
                this.logList.Add(line);

                try
                {
                    Message msg = MessageFactory.create(MessageFactory.getMessageName(line), line);
                    if (msg is GameStateMessage)
                    {
                        this.turnToLogLine.Add((msg as GameStateMessage).turn, this.logList.Count - 1);
                        if ((msg as GameStateMessage).turn < minTurn) minTurn = (msg as GameStateMessage).turn;
                        if ((msg as GameStateMessage).turn > maxTurn) maxTurn = (msg as GameStateMessage).turn;
                    }

                    if (msg is NewEffectsMessage)
                    {
                        foreach (EffectMessage current in NewEffectsMessage.parseEffects(msg.getRawText()))
                        {
                            if (current.type == "TurnBegin")
                            {
                                this.turnToLogLine.Add((current as EMTurnBegin).turn,this.logList.Count-1);
                                if ((current as EMTurnBegin).turn < minTurn) minTurn = (current as EMTurnBegin).turn;
                                if ((current as EMTurnBegin).turn > maxTurn) maxTurn = (current as EMTurnBegin).turn;


                            }
                        }
                    }
                }
                catch { }

                jsonms.runParsing();
                line = jsonms.getNextMessage();
            }

            /*foreach (KeyValuePair<int, int> kvp in this.turnToLogLine)
            {
                Console.WriteLine("turn " + kvp.Key + " line " + kvp.Value);
                Console.WriteLine(logList[kvp.Value]);
            }
            */
            Console.WriteLine("# minturn: " +this.minTurn + ", maxturn: " + this.maxTurn);
            /*
            Console.WriteLine("FIX ID "+line);
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
				} catch {
				}
				jsonms.runParsing();
				line = jsonms.getNextMessage();
			}
			if (realID != null) {
                Console.WriteLine("replace" + realID + "with" + App.MyProfile.ProfileInfo.id);
				log = log.Replace (realID, App.MyProfile.ProfileInfo.id);
			}
            */
            App.SceneValues.battleMode = new SceneValues.SV_BattleMode(GameMode.Replay);//GameMode.Play
			SceneLoader.loadScene("_BattleModeView");


            //jsonms.clear();
            //jsonms.feed(log);
            //jsonms.runParsing();
            //Console.WriteLine("Playing:\r\n" + log);
            //line = jsonms.getNextMessage();

            this.readedGameState = false;
            int lineindex = 0;
            line = this.logList[lineindex];

            

			while (playing)
			{
                
                Console.WriteLine("# nxt replay mssg: "+ lineindex +" "+line );
                Message msg = MessageFactory.create(MessageFactory.getMessageName(line), line);

                if (msg is CardInfoMessage) // CardInfoMessages are not very informative for players :D
                {
                    lineindex++;
                    line = this.logList[lineindex];
                    continue;
                }

                if (msg is GameStateMessage && this.readedGameState)
                {
                    lineindex++;
                    line = this.logList[lineindex];
                    continue;
                }
                else { if (msg is GameStateMessage) this.readedGameState = true; }


                dispatchMessages.Invoke(App.Communicator, new object[] { msg });// <---the whole magic

                if (line.Contains("EndGame") && !(msg is GameChatMessageMessage) && !(msg is RoomChatMessageMessage))
                {
                    playing = false;

                }
                else
                {
                        //jsonms.runParsing();
                        //line = jsonms.getNextMessage();
                    lineindex++;
                    line = this.logList[lineindex];
                }
                readNextMsg = false;
                if (msg is GameChatMessageMessage || msg is PingMessage ) readNextMsg = true;
				if (readNextMsg == false)
				{
					//delay messages otherwise the game rushes through in about a minute.

                    if (msg is GameStateMessage || msg is NewEffectsMessage)
                    {
                        List<EffectMessage> effects = ((List<EffectMessage>)effectsField.GetValue(this.bm));
                        while (effects.Count >= 1)
                        {
                            Thread.Sleep(100);
                            effects = ((List<EffectMessage>)effectsField.GetValue(this.bm));
                        }
                    }
                    else
                    {
                        Thread.Sleep(2000);
                    }
					while (paused)
					{
						Thread.Sleep(1000);
					}
					//readNextMsg = true;
				}
                if (this.seekTurn >= 1)
                {
                    try
                    {
                        lineindex = this.turnToLogLine[this.seekTurn];
                        line = this.logList[lineindex];
                    }
                    catch { Console.WriteLine("cant find turn"); }
                    this.seekTurn = 0;
                }
			}
            Console.WriteLine("player stoped");

		}

		public void AfterInvoke(InvocationInfo info, ref object returnValue)
		{
            if(info.target is BattleMode && info.targetMethod.Equals("OnGUI") && playing)
            {
                int depth = GUI.depth;

                // Container
                Color color = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 1f);
                Rect container = new Rect((float)(Screen.width * 0.10f), (float)Screen.height * (0.84f + 0.032f) + 10f, (float)(Screen.width * 0.08f), (float)Screen.height * 0.16f * 0.40f +12f);
                GUI.DrawTexture(container, ResourceManager.LoadTexture("Shared/blackFiller"));
                GUI.color = color;

                GUI.depth = depth - 4;

                // Start/Pause
                Rect pos = new Rect(container.x + 3f, container.y + 3f, container.width - 6f, (float)Screen.height * 0.16f * 0.20f);
                //pos = new Rect(container.x * 1.06f, pos.y + pos.height - 6f, container.width * 0.90f, container.height * 0.0f);
                if (GUI.Button(pos, paused ? "Play" : "Pause", this.buttonStyle))
                {
                    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");

                    paused = !paused;
                }

                // Go to Round
                Rect goToPos = new Rect(pos.x, pos.yMax + 6f, pos.width, (float)Screen.height * 0.16f * 0.20f);

                String label = "Go To";
                if (seekTurn > 0)
                {
                    label = "Going";
                }

                if (GUI.Button(goToPos, label, this.buttonStyle))
                {
                    paused = true;
                    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                    string enterturnstring = "Enter a Turn between " + this.minTurn + " and " + this.maxTurn + ":";
                    App.Popups.ShowTextInput(this, "", "Turn 1 = First Player Round 1 / Turn 2 = Second Player Round 1 / Turn 3 = First Player Round 2 and so on.", "turn", "Turn Seek", enterturnstring, "Seek");
                }

            }
            /*
            if (info.target is BattleModeUI)
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
                                //App.ChatUI.SetEnabled(true);
                                //App.ChatUI.SetLocked(false);
                                //App.ChatUI.Show(false);
                                //App.ChatUI.SetCanOpenContextMenu(false);
                                //activate chat on replays but disable profile or trading menus (wired bugs)
                            }
                        }
                        break;
                    case "Raycast":
                        {
                            endGameButton = ((GameObject)typeof(BattleModeUI).GetField("endTurnButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(info.target));
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
            }*/

		}

        /*
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
      */


        // Round seeking
        public void PopupCancel(String type)
        {

        }

        public void PopupOk(String type)
        {

        }

        public void PopupOk(String type, String choice)
        {
            if (type == "turn")
            {
                seekTurn = Convert.ToInt16(choice);
                if (seekTurn <= this.minTurn)
                    seekTurn = this.minTurn;
                if (seekTurn >= this.maxTurn)
                    seekTurn = this.maxTurn;
                paused = false;
                this.readedGameState = false;
                
            }
        }




	}
}

