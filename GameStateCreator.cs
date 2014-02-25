using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace GameReplay.Mod
{
    class GameStateCreator
    {
        private int[] whiteIdols = { 10, 10, 10, 10, 10 };
        private int[] blackIdols = { 10, 10, 10, 10, 10 };
        private int[] whiteIdolsMax = { 10, 10, 10, 10, 10 };
        private int[] blackIdolsMax = { 10, 10, 10, 10, 10 };
        private int turnnumber = 1;

        private FieldInfo leftPlayerField = typeof(BattleMode).GetField("leftPlayerName", BindingFlags.NonPublic | BindingFlags.Instance);
        private FieldInfo gameTypeField = typeof(BattleMode).GetField("gameType", BindingFlags.NonPublic | BindingFlags.Instance);
        //private MethodInfo unitsMethod = typeof(BattleMode).GetMethod("getUnitsFor", BindingFlags.NonPublic | BindingFlags.Instance);

        public string whitePlayerName = "me";
        public string blackPlayerName = "you";
        // Creates a GameState Message 
        public string create(BattleMode bm, BattleModeUI bmUI, bool whitesTurn)
        {
            string leftPlayerName = (string)leftPlayerField.GetValue(bm);
            //string blackPlayerName = ((string)typeof(BattleMode).GetField("rightPlayerName", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bm));
            //TileColor activeColor = ((TileColor)typeof(BattleMode).GetField("activeColor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bm));
            //int turnNumber = ((int)typeof(BattleMode).GetField("currentTurn", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bm));
            GameType gameType = (GameType)gameTypeField.GetValue(bm);
            int secondsleft = -1;
            if (gameType == GameType.MP_RANKED) secondsleft = 90;
            if (gameType == GameType.MP_QUICKMATCH) secondsleft = 60;
            if (gameType == GameType.MP_LIMITED) secondsleft = 90;

            TileColor activeColor = TileColor.white;
            if (!whitesTurn) activeColor = TileColor.black;

            PlayerAssets whiteplayer = bmUI.GetResources(true);
            PlayerAssets blackplayer = bmUI.GetResources(false);
            if (leftPlayerName == blackPlayerName) 
            {
                whiteplayer = bmUI.GetResources(false);
                blackplayer = bmUI.GetResources(true);
            }

            //ResourceGroup whiteRessisAvail = ((ResourceGroup)typeof(BattleModeUI).GetField("leftAvailable", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bmUI));
            //ResourceGroup blackRessisAvail = ((ResourceGroup)typeof(BattleModeUI).GetField("rightAvailable", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bmUI));
            //ResourceGroup whiteRessisMax = ((ResourceGroup)typeof(BattleModeUI).GetField("leftMax", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bmUI));
            //ResourceGroup blackRessisMax = ((ResourceGroup)typeof(BattleModeUI).GetField("rightMax", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bmUI));

            string retval = "";
            //{"whiteGameState":{"playerName":"Easy AI","board":{"color":"white","tiles":[{"card":{"id":7837,"typeId":127,"tradable":true,"isToken":false,"level":0},"ap":4,"ac":2,"hp":3,"position":"1,0","buffs":[{"name":"Crown of Strength","description":"Enchanted unit gains +1 Attack and +2 Health.","type":"ENCHANTMENT"}]},{"card":{"id":7834,"typeId":126,"tradable":true,"isToken":false,"level":0},"ap":1,"ac":2,"hp":2,"position":"1,1"},{"card":{"id":7838,"typeId":127,"tradable":true,"isToken":false,"level":0},"ap":3,"ac":2,"hp":3,"position":"1,2"}],"idols":[10,10,0,10,9]},"assets":{"availableResources":{"DECAY":0,"ORDER":4,"ENERGY":0,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":5,"ENERGY":0,"GROWTH":0},"handSize":4,"librarySize":30,"graveyardSize":12}},"blackGameState":{"playerName":"fuj1n","board":{"color":"black","tiles":[{"card":{"id":6151538,"typeId":68,"tradable":false,"isToken":false,"level":0},"ap":5,"ac":1,"hp":4,"position":"2,1"},{"card":{"id":6151539,"typeId":68,"tradable":false,"isToken":false,"level":0},"ap":5,"ac":1,"hp":4,"position":"2,2"}],"idols":[10,6,10,10,10]},"assets":{"availableResources":{"DECAY":0,"ORDER":0,"ENERGY":6,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":0,"ENERGY":6,"GROWTH":0},"handSize":3,"librarySize":26,"graveyardSize":19}},"activeColor":"black","phase":"Main","turn":26,"hasSacrificed":false,"secondsLeft":-1,"msg":"GameState"}

            retval = "{\"whiteGameState\":{\"playerName\":\"" + whitePlayerName + "\",\"board\":{\"color\":\"white\",\"tiles\":[";
            //get cards:
            retval = retval + this.getTiles(bm, true);
            //],"idols":[10,10,0,10,9]},
            //get white idols
            retval = retval + "],\"idols\":[" + this.whiteIdols[0] + "," + this.whiteIdols[1] + "," + this.whiteIdols[2] + "," + this.whiteIdols[3] + "," + this.whiteIdols[4] + "]},";
            //board finished
            //"assets":{"availableResources":{"DECAY":0,"ORDER":4,"ENERGY":0,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":5,"ENERGY":0,"GROWTH":0},
            retval = retval + "\"assets\":{\"availableResources\":{\"DECAY\":" + whiteplayer.availableResources.DECAY + ",\"ORDER\":" + whiteplayer.availableResources.ORDER + ",\"ENERGY\":" + whiteplayer.availableResources.ENERGY + ",\"GROWTH\":" + whiteplayer.availableResources.GROWTH + "},";
            retval = retval + "\"outputResources\":{\"DECAY\":" + whiteplayer.outputResources.DECAY + ",\"ORDER\":" + whiteplayer.outputResources.ORDER + ",\"ENERGY\":" + whiteplayer.outputResources.ENERGY + ",\"GROWTH\":" + whiteplayer.outputResources.GROWTH + "},";
            //"handSize":4,"librarySize":30,"graveyardSize":12}},
            retval = retval + "\"handSize\":" + whiteplayer.handSize + ",\"librarySize\":" + whiteplayer.librarySize +",\"graveyardSize\":" + whiteplayer.graveyardSize +"}},";
            
            //black
            //"blackGameState":{"playerName":"Easy AI","board":{"color":"black","tiles":[{"card":{"id":6151538,"typeId":68,"tradable":false,"isToken":false,"level":0},"ap":5,"ac":1,"hp":4,"position":"2,1"},{"card":{"id":6151539,"typeId":68,"tradable":false,"isToken":false,"level":0},"ap":5,"ac":1,"hp":4,"position":"2,2"}
            retval = retval + "\"blackGameState\":{\"playerName\":\"" + blackPlayerName + "\",\"board\":{\"color\":\"black\",\"tiles\":[";

            retval = retval + this.getTiles(bm, false);
            // ],"idols":[10,6,10,10,10]},
            retval = retval + "],\"idols\":[" + this.blackIdols[0] + "," + this.blackIdols[1] + "," + this.blackIdols[2] + "," + this.blackIdols[3]  +"," + this.blackIdols[4] + "]},";
            //board finished
            //"assets":{"availableResources":{"DECAY":0,"ORDER":0,"ENERGY":6,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":0,"ENERGY":6,"GROWTH":0},
            retval = retval + "\"assets\":{\"availableResources\":{\"DECAY\":" + blackplayer.availableResources.DECAY + ",\"ORDER\":" + blackplayer.availableResources.ORDER + ",\"ENERGY\":" + blackplayer.availableResources.ENERGY + ",\"GROWTH\":" + blackplayer.availableResources.GROWTH + "},";
            retval = retval + "\"outputResources\":{\"DECAY\":" + blackplayer.outputResources.DECAY + ",\"ORDER\":" + blackplayer.outputResources.ORDER + ",\"ENERGY\":" + blackplayer.outputResources.ENERGY + ",\"GROWTH\":" + blackplayer.outputResources.GROWTH + "},";
            //"handSize":3,"librarySize":26,"graveyardSize":19}},
            retval = retval + "\"handSize\":" + blackplayer.handSize + ",\"librarySize\":" + blackplayer.librarySize + ",\"graveyardSize\":" + blackplayer.graveyardSize + "}},";
            //"activeColor":"black","phase":"Main","turn":26,"hasSacrificed":false,"secondsLeft":-1,"msg":"GameState"}

            retval = retval + "\"activeColor\":\"" + activeColor.ToString() +"\",\"phase\":\"Main\",\"turn\":"+ this.turnnumber + ",\"hasSacrificed\":false,\"secondsLeft\":" + secondsleft + ",\"msg\":\"GameState\"}";
            
            return retval;
        }

        //{"whiteGameState":{"playerName":"Eva","board":{"color":"white","tiles":[{"card":{"id":4269572,"typeId":153,"tradable":true,"isToken":false,"level":2},"ap":0,"ac":4,"hp":5,"position":"4,0"},{"card":{"id":18154143,"typeId":207,"tradable":true,"isToken":false,"level":0},"ap":0,"ac":2,"hp":5,"position":"0,2"},{"card":{"id":18628883,"typeId":204,"tradable":true,"isToken":false,"level":0},"ap":8,"ac":6,"hp":4,"position":"4,2"}],"idols":[10,10,4,7,7]},"assets":{"availableResources":{"DECAY":0,"ORDER":0,"ENERGY":0,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":3,"ENERGY":7,"GROWTH":0},"handSize":3,"librarySize":11,"graveyardSize":33}},"blackGameState":{"playerName":"iScrE4m","board":{"color":"black","tiles":[{"card":{"id":13865404,"typeId":205,"tradable":true,"isToken":false,"level":2},"ap":1,"ac":1,"hp":2,"position":"1,0"},{"card":{"id":-1,"typeId":96,"tradable":true,"isToken":true,"level":0},"ap":2,"ac":2,"hp":3,"position":"3,0"},{"card":{"id":21239788,"typeId":256,"tradable":true,"isToken":false,"level":0},"ap":2,"ac":6,"hp":3,"position":"1,2"},{"card":{"id":23944590,"typeId":281,"tradable":true,"isToken":false,"level":0},"ap":2,"ac":2,"hp":2,"position":"3,2"}],"idols":[10,10,10,10,10]},"assets":{"availableResources":{"DECAY":0,"ORDER":0,"ENERGY":2,"GROWTH":0},"outputResources":{"DECAY":0,"ORDER":0,"ENERGY":7,"GROWTH":0},"handSize":1,"librarySize":21,"graveyardSize":25}},"activeColor":"black","phase":"PreMain","turn":26,"hasSacrificed":false,"secondsLeft":90,"msg":"GameState"}



        private string getTiles(BattleMode bm, bool iswhite)
        {
            TileColor tc = TileColor.black;
            if (iswhite) tc = TileColor.white;
            List<Unit> units = bm.getUnitsFor(tc);//(List<Unit>)unitsMethod.Invoke(bm, new object[] { tc });
            string retval = "";
            //{"card":{"id":7837,"typeId":127,"tradable":true,"isToken":false,"level":0},"ap":4,"ac":2,"hp":3,"position":"1,0","buffs":[{"name":"Crown of Strength","description":"Enchanted unit gains +1 Attack and +2 Health.","type":"ENCHANTMENT"}]}
            foreach (Unit u in units)
            {
                Card c = u.getCard();
                if (retval != "") retval = retval + ",";
                //{"card":{"id":7837,"typeId":127,"tradable":true,"isToken":false,"level":0},
                retval = retval + "{\"card\":{\"id\":"+ c.getId()+ ",\"typeId\":"+ c.getType() + ",\"tradable\":"+c.tradable.ToString().ToLower()+",\"isToken\":"+c.isToken.ToString().ToLower()+",\"level\":"+c.level+"},";
                //"ap":4,"ac":2,"hp":3,"position":"1,0"
                retval = retval + "\"ap\":" + u.getAttackPower() + ",\"ac\":" + u.getAttackInterval() + ",\"hp\":" + u.getHitPoints() + ",\"position\":\"" + u.getTilePosition().row + "," + u.getTilePosition().column + "\"";
                //,"buffs":[{"name":"Crown of Strength","description":"Enchanted unit gains +1 Attack and +2 Health.","type":"ENCHANTMENT"}]
                List<EnchantmentInfo> b = u.getBuffs();
                if (b.Count >= 1)
                {
                    retval = retval + ",\"buffs\":[";
                    string buffes="";
                    foreach (EnchantmentInfo e in b)
                    {
                        if (buffes != "") buffes = buffes + ",";
                        buffes = buffes + "{\"name\":\"" + e.name + "\",\"description\":\"" + e.description + "\",\"type\":\"" + e.type.ToString().ToUpper() + "\"}";
                    }
                    retval = retval + buffes+ "]";
                }
                retval = retval + "}";
            }

            return retval;
        }

        public void updateIdols(List<EffectMessage> upid)
        {
            foreach (EffectMessage em in upid)
            {
                IdolInfo ium = (em as EMIdolUpdate).idol;
                if (ium.color == TileColor.white) { this.whiteIdols[ium.position] = ium.hp; }
                else { this.blackIdols[ium.position] = ium.hp; }
            }
        
        }

        public void updateIdols(IdolInfo[] upid)
        {
            foreach (IdolInfo ium in upid)
            {
                if (ium.color == TileColor.white) { this.whiteIdols[ium.position] = ium.hp; }
                else { this.blackIdols[ium.position] = ium.hp; }
            }

        }

        public void GameStateupdateIdols(int[] idolhp, bool iswhite)
        {
            for (int i = 0; i < idolhp.Length; i++)
            {
                if (iswhite) { this.whiteIdols[i] = idolhp[i]; }
                else { this.blackIdols[i] = idolhp[i]; }
            }

        }

        public void setTurn(int t) { this.turnnumber = t; }


    }
}
