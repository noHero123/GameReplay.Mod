using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using Mono.Cecil;
using UnityEngine;
using ScrollsModLoader.Interfaces;
using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using JsonFx.Json;

namespace GameReplay.Mod
{
	public class Mod : BaseMod, ICommListener, IListCallback, IOkStringCancelCallback, IOkCallback
	{
		private Recorder recorder = null;
		private Player player;
		private UIListPopup recordListPopup;
		List<Item> recordList = new List<Item>();
		private Record selectedRecord;
        private Settings sttngs;
		private String recordFolder;
        private bool spectating = false;
        private BattleMode bm = null;
        private BattleModeUI bmui = null;

        private GUISkin guiSkin;

        private FieldInfo currentEffectField=typeof(BattleMode).GetField("currentEffect", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo frameRectField = typeof(ProfileMenu).GetField("frameRect", BindingFlags.Instance | BindingFlags.NonPublic);
        private FieldInfo showEditField = typeof(ProfileMenu).GetField("showEdit", BindingFlags.Instance | BindingFlags.NonPublic);
		//private MethodInfo getButtonRectMethod= typeof(ProfileMenu).GetMethod("getButtonRect", BindingFlags.NonPublic | BindingFlags.Instance);
        //private FieldInfo guiSkinField=typeof(ProfileMenu).GetField("guiSkin", BindingFlags.Instance | BindingFlags.NonPublic);
        private FieldInfo usernameStyleField=typeof(ProfileMenu).GetField("usernameStyle", BindingFlags.Instance | BindingFlags.NonPublic);

        public Mod()
		{
            this.guiSkin = (GUISkin)Resources.Load("_GUISkins/Lobby");
			//recordFolder = this.OwnFolder() + Path.DirectorySeparatorChar + "Records";
            string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||Environment.OSVersion.Platform == PlatformID.MacOSX)? Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
            recordFolder = homePath + Path.DirectorySeparatorChar + "ScrollsRecords";

            if (!Directory.Exists(recordFolder + Path.DirectorySeparatorChar))
			{
				Directory.CreateDirectory(recordFolder + Path.DirectorySeparatorChar);
			}
			player = new Player(recordFolder);
            sttngs = new Settings(recordFolder);

			try {
				App.Communicator.addListener(this);
			} catch {}

			new Thread (new ThreadStart (parseRecords)).Start ();
            Console.WriteLine("loaded Recorder");
		}

		public static string GetName()
		{
			return "GameReplay";
		}

		public static int GetVersion()
		{
			return 12;
		}

		public void handleMessage(Message msg)
		{
			try {
				if (msg is BattleRedirectMessage)
				{
                    this.spectating = false;
					recorder = new Recorder(recordFolder, this, this.sttngs);
					Console.WriteLine("Recorder started: " + recorder);
				}
			} catch {}
            //try
            //{
                if (msg is SpectateRedirectMessage)
                {
                    this.spectating = true;
                    recorder = new Recorder(recordFolder, this, this.sttngs);
                    Console.WriteLine("Recorder started: " + recorder + " (Spectator)");
                    
                }
            //}
            //catch { }
		}

		public void onConnect(OnConnectData ocd)
		{
			return;
		}

		public String getRecordFolder()
		{
			return recordFolder;
		}

		public Player getPlayer()
		{
			return player;
		}
		
		public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version)
		{
			try {
				MethodDefinition[] defs = new MethodDefinition[] {
					scrollsTypes["ProfileMenu"].Methods.GetMethod("Start")[0],
					scrollsTypes["ProfileMenu"].Methods.GetMethod("getButtonRect", new Type[]{typeof(int)}),
					scrollsTypes["ProfileMenu"].Methods.GetMethod("drawEditButton")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("Start")[0],
                    scrollsTypes["BattleMode"].Methods.GetMethod("effectDone")[0],
                    scrollsTypes["BattleModeUI"].Methods.GetMethod("Start")[0],
                    scrollsTypes["SettingsMenu"].Methods.GetMethod("OnGUI")[0],
                    scrollsTypes["Communicator"].Methods.GetMethod("joinLobby", new Type[]{typeof(bool)}),
                    
				};

				List<MethodDefinition> list = new List<MethodDefinition>(defs);
				list.AddRange(Player.GetPlayerHooks(scrollsTypes, version));

				return list.ToArray();
			} catch {
				return new MethodDefinition[] {};
			}
		}

		public override bool WantsToReplace (InvocationInfo info)
		{
			if (info.targetMethod.Equals ("getButtonRect")) {

				foreach (StackFrame frame in info.stackTrace.GetFrames()) {
					if (frame.GetMethod ().Name.Contains ("BeforeInvoke")) {
                        break;
					}
					if (frame.GetMethod ().Name.Contains ("drawEditButton")) {
						return true;
					}
				}
			}
			return player.WantsToReplace(info);
		}

		public override void ReplaceMethod (InvocationInfo info, out object returnValue) {
            if (info.target is ProfileMenu && info.targetMethod.Equals("getButtonRect"))
            {
                returnValue = (Rect)typeof(ProfileMenu).GetMethod("getButtonRect", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(info.target, new object[] { 2 });
            }
            else
            {
                player.ReplaceMethod(info, out returnValue);
            }
		}

		public override void BeforeInvoke(InvocationInfo info)
		{
			if (info.target is ProfileMenu && info.targetMethod.Equals("Start") && App.SceneValues.profilePage.isMe())
			{
				//list them
				//recordList.Clear();

				recordListPopup = new GameObject("Record List").AddComponent<UIListPopup>();
				recordListPopup.transform.parent = ((ProfileMenu)info.target).transform;
                Rect frame = (Rect)frameRectField.GetValue(info.target);
				recordListPopup.Init(new Rect(frame.center.x - frame.width / 2.0f, frame.center.y - frame.height / 2.0f + 32.0f, frame.width, frame.height - (float)Screen.height * 0.055f * 3.0f), false, true, recordList, this, null, null, false, true, false, true, null, true, false);
				recordListPopup.enabled = true;
				recordListPopup.SetOpacity(1f);
			}
            if (info.target is ProfileMenu &&  info.targetMethod.Equals("drawEditButton"))
            {
                Rect rect = (Rect)frameRectField.GetValue(info.target);
				//LobbyMenu.drawShadowText (new Rect(rect.center.x-(float)Screen.width/8.0f/2.0f, rect.center.y-rect.height/2.0f-(float)Screen.height*0.055f*3.0f-40.0f, (float)Screen.width/8.0f, 35.0f), "Match History", Color.white);

                if ((bool)showEditField.GetValue(info.target))
				{
					recordListPopup.enabled = false;
					recordListPopup.SetOpacity(0f);
				}
				else
				{
					new ScrollsFrame(new Rect(rect.center.x - rect.width / 2.0f - 20.0f, rect.center.y - rect.height / 2.0f, rect.width + 40.0f, rect.height - (float)Screen.height * 0.055f - 20.0f)).AddNinePatch(ScrollsFrame.Border.DARK_CURVED, NinePatch.Patches.CENTER).Draw();
					recordListPopup.enabled = true;
					recordListPopup.SetOpacity(1f);
                    GUIStyle labelSkin = (GUIStyle)usernameStyleField.GetValue(info.target);
					labelSkin.fontSize = 32;
					GUI.Label(new Rect(rect.center.x - (float)Screen.width / 6.0f / 2.0f, rect.center.y - rect.height / 2.0f - 40.0f, (float)Screen.width / 6.0f, 35.0f), "Match History", labelSkin);
                    if (LobbyMenu.drawButton((Rect)this.sttngs.getButtonRect(0), "Load Replay", this.guiSkin ))//  getButtonRectMethod.Invoke(info.target, new object[] { 0 }) ,  (GUISkin)guiSkinField.GetValue(info.target)
					{
						LoadReplay();
					}
                    
				}
			}
            if (info.target is BattleMode && info.targetMethod.Equals("effectDone"))
            {
                    EffectMessage currentEffect = ((EffectMessage)currentEffectField.GetValue(info.target));
                    if (currentEffect != null && currentEffect.type == "TurnBegin" && currentEffect.getRawText() != null && currentEffect.getRawText().Contains("{\"TurnBegin\":") && recorder != null && recorder.recording == true)
                    {
                        //Console.WriteLine("turnbegin end of: " + currentEffect.getRawText()); //{"TurnBegin":
                        recorder.turnBeginEnds();
                    }

            }

			if (!(info.target is ProfileMenu))
				player.BeforeInvoke(info);
		}

        


		public override void AfterInvoke(InvocationInfo info, ref object returnValue)
        {
            if (info.target is SettingsMenu && info.targetMethod.Equals("OnGUI"))
            {

                string label = "ask after match";
                if (!this.sttngs.alwayssave) label = "never ask";
                if (LobbyMenu.drawButton((Rect)this.sttngs.getButtonRect(1), label, this.guiSkin))//(GUISkin)guiSkinField.GetValue(info.target)
                {
                    this.sttngs.alwayssave = !this.sttngs.alwayssave;
                    this.sttngs.saveSettings();
                }
            }
            if (info.target is Communicator && info.targetMethod.Equals("joinLobby") && recorder != null && recorder.recording == true)
            {
                recorder.stoprecording();
            }
            if (info.target is BattleMode && info.targetMethod.Equals("Start"))
            {
                player.setbm(info.target as BattleMode);
            }
            if (info.target is BattleMode && info.targetMethod.Equals("Start") && this.player.playing)
            {
                App.ChatUI.Show(false);
                App.ChatUI.SetMode(OnlineState.SPECTATE);
                App.ChatUI.SetLocked(false, (float)Screen.height * 0.25f);
                App.Communicator.setEnabled(true, true);
                App.ChatUI.SetEnabled(true);
                Console.WriteLine("playing + battlemodestart");
            }
            if (info.target is BattleMode && info.targetMethod.Equals("Start") && recorder != null && recorder.recording == true)
            {
                Console.WriteLine("## set Bm");
                recorder.setBm(info.target as BattleMode);
                if (this.spectating)
                {
                    recorder.recordSpectator();
                    Console.WriteLine("## (Spectator)");
                }
            }


            if (info.target is BattleModeUI && info.targetMethod.Equals("Start") && recorder != null && recorder.recording == true)
            {
                Console.WriteLine("## set Bmui");
                recorder.setBmUI(info.target as BattleModeUI);
                
            }

            if (!(info.target is BattleMode) || (info.target is BattleMode && (!info.targetMethod.Equals("Start") && !info.targetMethod.Equals("effectDone"))) || (info.target is Communicator && !info.targetMethod.Equals("joinLobby")))
            {
                player.AfterInvoke(info, ref returnValue);
            }
        }

		public void ButtonClicked(UIListPopup popup, ECardListButton button)
		{
			return;
		}

		public void ButtonClicked(UIListPopup popup, ECardListButton button, List<Item> selectedCards)
		{
			return;
		}

		public void ItemButtonClicked(UIListPopup popup, Item card)
		{
			return;
		}

		public void ItemClicked(UIListPopup popup, Item card)
		{
			//player.LaunchReplay (((Record)card).fileName());
			selectedRecord = (Record)card;
			//App.Popups.ShowMultibutton(this, "Replayer", card.getDesc(), new string[] { "Play", "Share", "Delete" });
            App.Popups.ShowMultibutton(this, "Replayer", card.getDesc(), new GUIContent[]
			{
				new GUIContent("Play"),
				new GUIContent("Share"),
				new GUIContent("Delete")
			});
		}

		public void ItemHovered(UIListPopup popup, Item card)
		{
			return;
		}

		public void LoadReplay()
		{
			//App.Popups.ShowMultibutton(this, "Load", "Load", new string[] { "From File", "From Link" });
            App.Popups.ShowMultibutton(this, "Load", "Load", new GUIContent[]
			{
				new GUIContent("From File"),
				new GUIContent("From Link")
			});
		}

		public void PopupOk(string popupType, string choice)
		{
			if (popupType.Equals("savedeck"))
			{
				Console.WriteLine("Loading replay " + choice);

				// try a few regexes until a match is found
				List<RegexPattern> patterns = new List<RegexPattern>();
				patterns.Add(new RegexPattern("^(http://)?(www\\.)?scrollsguide\\.com/replays/r/([0-9]+)$", 3)); // full replay url in browser
				patterns.Add(new RegexPattern("^([0-9]+)$", 1)); // just the replay number
				patterns.Add(new RegexPattern("^http://a\\.scrollsguide\\.com/replay/download/([0-9]+)(\\?true)?$", 1)); // full replay url with or without ?true
				bool hasMatch = false;
				for (int i = 0; i < patterns.Count && !hasMatch; i++)
				{
					RegexPattern pattern = patterns[i];
					Match m = Regex.Match(choice, pattern.getPattern());
					if (m.Success)
					{
						Console.WriteLine("Regex match: " + m.Groups[pattern.getMatchNum()].Value);

						hasMatch = true;

						ReplayDownloader rd = new ReplayDownloader(this);
						rd.startDownload(Convert.ToInt32(m.Groups[pattern.getMatchNum()].Value));
					}
					else
					{
						Console.WriteLine("Can't match replay regex for " + pattern.getPattern());
					}
				}
				if (!hasMatch)// is not a valid replay url
				{
					App.Popups.ShowOk(this, "fail", "Invalid url", "That is not a valid url, replay can't be loaded.", "Ok");
				}
			}
			else
			{
				if (choice.Equals ("Play")) {
					player.LaunchReplay (selectedRecord.fileName ());
				} else if (choice.Equals ("Share")) {
					new ReplayUploader (this, selectedRecord);
				} else if (choice.Equals ("Delete")) {
					File.Delete (selectedRecord.fileName ());
					recordList.Remove(selectedRecord);
					recordListPopup.SetItemList (recordList);
				}
				else if (choice.Equals("From File"))
				{
					String path = modAPI.FileOpenDialog ();
					player.LaunchReplay (path);
				}
				else if (choice.Equals("From Link"))
				{
					//App.Popups.ShowTextInput(this, "", "Browse replays on http://www.scrollsguide.com/replays", "savedeck", "Load replay", "Paste the url of the replay:", "Load");
                    App.Popups.ShowTextEntry(this, "savedeck", "Load replay", "Paste the url of the replay:", "Load", "Cancel", "");
				}
			}
		}
		public void PopupCancel(string popupType)
		{
			return;
		}

		// method for single OKButton popup
		public void PopupOk(string popupType)
		{
			return;
		}

		public void parseRecords() {
			foreach (String file in Directory.GetFiles(recordFolder))
			{
				if (file.EndsWith("sgr"))
				{
					parseRecord (file);
				}
			}
		}

		public void parseRecord(String file) {
			JsonMessageSplitter jsonms = new JsonMessageSplitter();
			String log = File.ReadAllText(file);
			jsonms.feed(log);
			jsonms.runParsing();
			String line = jsonms.getNextMessage();
			bool searching = true;
			String player1name = null;
			String player2name = null;
			//String enemyId = null;
			String deckName = null;
			ResourceType type = ResourceType.NONE;

			while (line != null && searching)
			{
				try
				{
                    Message msg = MessageFactory.create(MessageFactory.getMessageName(line), line);
					if (msg is GameInfoMessage)
					{
						GameInfoMessage gim = (GameInfoMessage) msg;
						if (gim.white.Equals(App.MyProfile.ProfileInfo.name))
						{
							player1name = gim.white;
							player2name = gim.black;
						}
						else 
						{
							player1name = gim.black;
							player2name = gim.white;
						}
						deckName = ""; //deckname isnt in gameinfomessage anymore
					}
					if (msg is ActiveResourcesMessage)
					{
						type = (msg as ActiveResourcesMessage).types[0];
					}
					if (player2name != null && type != ResourceType.NONE)
					{
						searching = false;
					}
				}
				catch
				{
				}
				jsonms.runParsing();
				line = jsonms.getNextMessage();
			}

			recordList.Add(new Record(File.GetCreationTime(file).ToShortDateString() + " - " + File.GetCreationTime(file).ToShortTimeString(),player1name +  " vs " + player2name + " - " + deckName, /*enemyId,*/ file, type));
			if (recordListPopup != null)
				recordListPopup.SetItemList (recordList);
		}

        

    
    }

	internal class RegexPattern
	{
		private String pattern;
		private int matchNum;

		public RegexPattern(String pattern, int matchNum)
		{
			this.pattern = pattern;
			this.matchNum = matchNum;
		}

		public String getPattern()
		{
			return this.pattern;
		}
		public int getMatchNum()
		{
			return this.matchNum;
		}
	}

	internal class Record : Item
	{
		private String Title;
		private String Description;
		private String filename;
		//private String enemyId;
		private ResourceType resource;

		public Record(String title, String desc, /*String enemyId,*/ String filename, ResourceType resource)
		{
			this.Title = title;
			this.Description = desc;
			this.filename = filename;
			this.resource = resource;
			//this.enemyId = enemyId;
		}

		public bool selectable()
		{
			return true;
		}

		public Texture getImage()
		{
			switch (resource)
			{
				case ResourceType.GROWTH:
					return ResourceManager.LoadTexture("BattleUI/battlegui_icon_growth");
				case ResourceType.ENERGY:
					return ResourceManager.LoadTexture("BattleUI/battlegui_icon_energy");
				case ResourceType.ORDER:
					return ResourceManager.LoadTexture("BattleUI/battlegui_icon_order");
				case ResourceType.DECAY:
					return ResourceManager.LoadTexture("BattleUI/battlegui_icon_decay");
				default:
					return null;
			}
		}

		public String getName()
		{
			return Title;
		}

		public String getDesc()
		{
			return Description;
		}

		public String fileName()
		{
			return filename;
		}

		public long getId()
		{
			return Convert.ToInt64(Path.GetFileNameWithoutExtension(filename));
		}
	}

	internal class ReplayUploader
	{
		private Mod m;
		private Record toUpload;

		public ReplayUploader(Mod m, Record r)
		{
			this.m = m;
			this.toUpload = r;

			Thread uploadThread = new Thread(new ThreadStart(Upload));
			uploadThread.Start();
		}

		private void Upload()
		{
			if (canShare())
			{
				NameValueCollection postParams = getPostParams();

				App.Popups.ShowInfo("Sharing", "Sharing your replay...");
				ResultMessage result = Extensions.HttpUploadFile("http://a.scrollsguide.com/replay/upload",
					toUpload.fileName(), "replay", "scr/replay", postParams);
				App.Popups.KillCurrentPopup();

				if (result.msg.Equals("success"))
				{
					App.Popups.ShowOk(m, "fail", "Replay shared", "Your replay has been shared. See it on scrollsguide.com/replays!", "Ok");
				}
				else
				{
					App.Popups.ShowOk(m, "fail", "Replay not shared", "There was an error sharing your replay: " + result.data, "Ok");
				}
			}
			else
			{
				App.Popups.ShowOk(m, "fail", "Replay not shared", "This replay was already shared.", "Ok");
			}
		}

		private NameValueCollection getPostParams()
		{
			NameValueCollection nvc = new NameValueCollection();
			nvc.Add("from", App.MyProfile.ProfileInfo.name); // player's username
			nvc.Add("gid", Convert.ToString(toUpload.getId())); // game id
			nvc.Add("mtime", Convert.ToString(Extensions.ToUnixTimestamp(File.GetCreationTimeUtc(toUpload.fileName())))); // creation time

			return nvc;
		}

		private bool canShare()
		{
			String html;
			try {
				html = new WebClient().DownloadString("http://a.scrollsguide.com/replay/canshare/" + toUpload.getId());
			} catch (WebException) {
				return false;
			}

			JsonReader r = new JsonReader();
			ResultMessage rm = r.Read(html, System.Type.GetType("ResultMessage")) as ResultMessage;

			return rm.data.Equals("yes");
		}
	}

	internal class ReplayDownloader
	{
		private WebClient wc = new WebClient();

		private Mod callback = null;

		private String saveDirectory;
		private String saveLocation;

		public ReplayDownloader(Mod p)
		{
			this.callback = p;
			
			saveDirectory = p.getRecordFolder() + Path.DirectorySeparatorChar + "downloads" + Path.DirectorySeparatorChar;
			if (!Directory.Exists(saveDirectory))
			{
				Directory.CreateDirectory(saveDirectory);
			}
		}

		public void startDownload(long gameId)
		{
			saveLocation = saveDirectory + gameId + ".sgr";

			if (File.Exists(saveLocation))
			{
				Console.WriteLine("Using cached replay file");
				callback.getPlayer().LaunchReplay(saveLocation);
			}
			else
			{
				Console.WriteLine("Downloading replay from internet");
				try {
					wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
					wc.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadFileCompleted);

					wc.DownloadFileAsync(new Uri("http://a.scrollsguide.com/replay/download/" + gameId + "?true"), saveLocation);
				} catch (WebException) {
					return;
				}
			}
		}

		void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
		}

		void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			Console.WriteLine("Done downloading :)");

			String[] keys = wc.ResponseHeaders.AllKeys;

			String contentType = "";
			for (int i = 0; i < keys.Length; i++)
			{
				Console.WriteLine(keys[i]);
				if (keys[i].Equals("Content-Type"))
				{
					contentType = wc.ResponseHeaders.Get(i);
				}
			}

			Console.WriteLine("Content-Type: " + contentType);

			System.Diagnostics.Debug.WriteLine("Done downloading the replay :)");

			if (contentType.Equals("scr/replay")) // only acceptable header, other headers are errors
			{
				callback.getPlayer().LaunchReplay(saveLocation);
			}
			else
			{
				File.Delete(saveLocation);
				App.Popups.ShowOk(callback, "fail", "Failed to download replay", "That's not a valid replay ID.", "Ok");
			}
		}
	}
}

