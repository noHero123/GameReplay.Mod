using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace GameReplay.Mod
{
	public class Recorder : ICommListener, IOkCancelCallback
	{
		private List<String> messages = new List<String>();
		private String saveFolder;
		private string gameID;
		private Mod uiClass;
		//private DateTime timestamp;
        private MiniCommunicator comm;
        public bool recording = false;

		public Recorder(String saveFolder, Mod uiClass)
		{
			this.saveFolder = saveFolder;
			//App.Communicator.addListener(this);
			this.comm = App.Communicator;
            this.comm.addListener(this);
                /*IpPort address = App.SceneValues.battleMode.address;
                if (!address.Equals(App.Communicator.getAddress()))
                {
                    this.comm = App.SceneValues.battleMode.specCommGameObject.GetComponent<MiniCommunicator>();
                    this.comm.addListener(this);
                    this.comm.setEnabled(true, true);
                }
                */
            this.uiClass = uiClass;
            this.recording = true;
			//timestamp = DateTime.Now;
		}

        public void recordSpectator() 
        {
            this.comm.removeListener(this);
            IpPort address = App.SceneValues.battleMode.address;
            if (!address.Equals(App.Communicator.getAddress()))
            {
                this.comm = App.SceneValues.battleMode.specCommGameObject.GetComponent<MiniCommunicator>();
                this.comm.addListener(this);
                this.comm.setEnabled(true, true);
            }
            messages.Add("{\"version\":\"0.112.1\",\"assetURL\":\"http://download.scrolls.com/assets/\",\"roles\":\"GAME,RESOURCE\",\"msg\":\"ServerInfo\"}");
        }

		public void handleMessage(Message msg)
		{
			try {
			if (msg is SpectateRedirectMessage ||msg is BattleRedirectMessage || msg is BattleRejoinMessage || msg is FailMessage || msg is OkMessage || msg is PingMessage || msg is WhisperMessage || msg is RoomInfoMessage) // whispers are private, pings are uncessary
			{
				return;
			}

            if (msg is HandViewMessage)
            {
                return;
            }

			if (msg is GameInfoMessage)
			{
				gameID = (msg as GameInfoMessage).gameId.ToString();
			}
            

			messages.Add(msg.getRawText());
            Console.WriteLine("REC "+msg.getRawText());

			if (msg is NewEffectsMessage && msg.getRawText().Contains("EndGame"))
			{
                this.recording = false;
				//save
                this.comm.removeListener(this);
                App.Popups.ShowOkCancel(this, "savereplay", "Save", "Want to save the recorded game?", "Ok", "No");

				//File.WriteAllLines(saveFolder + Path.DirectorySeparatorChar + gameID + ".sgr", messages.ToArray());
				//uiClass.parseRecord (saveFolder + Path.DirectorySeparatorChar + gameID + ".sgr");
                
               
			}

			

			//TO-DO:
			//steaming
			} catch {}
		}
		public void onConnect(OnConnectData ocd)
		{
			return; //I (still) don't care
		}

        public void stoprecording()
        {
            this.recording = false;
            //save
            messages.Add("{\"effects\":[{\"EndGame\":{\"winner\":\"black\",\"whiteStats\":{\"profileId\":\"RobotEasy\",\"idolDamage\":0,\"unitDamage\":0,\"unitsPlayed\":0,\"spellsPlayed\":0,\"enchantmentsPlayed\":0,\"scrollsDrawn\":0,\"totalMs\":1,\"mostDamageUnit\":0,\"idolsDestroyed\":0},\"blackStats\":{\"profileId\":\"RobotEasy\",\"idolDamage\":0,\"unitDamage\":0,\"unitsPlayed\":0,\"spellsPlayed\":0,\"enchantmentsPlayed\":0,\"scrollsDrawn\":0,\"totalMs\":1,\"mostDamageUnit\":0,\"idolsDestroyed\":0},\"whiteGoldReward\":{\"matchReward\":0,\"matchCompletionReward\":0,\"idolsDestroyedReward\":0,\"totalReward\":0},\"blackGoldReward\":{\"matchReward\":0,\"matchCompletionReward\":0,\"idolsDestroyedReward\":0,\"totalReward\":0}}}],\"msg\":\"NewEffects\"}");
            this.comm.removeListener(this);
            App.Popups.ShowOkCancel(this, "savereplay", "Save", "Want to save the recorded game?", "Ok", "No");

            
            
        }

        public void PopupOk(string s)
        {
            File.WriteAllLines(saveFolder + Path.DirectorySeparatorChar + gameID + ".sgr", messages.ToArray());
            uiClass.parseRecord(saveFolder + Path.DirectorySeparatorChar + gameID + ".sgr");
            Console.WriteLine("Save Recorded Game");
        }

        public void PopupCancel(string s)
        { 
        }


	}
}

