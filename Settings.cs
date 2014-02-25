using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

namespace GameReplay.Mod
{
    public class Settings
    {


        int frameBoxY = 260;
        int border = 60;
        Rect frameMockRect = new Rect();
        MockupCalc mockJunk1280 = new MockupCalc(1920, 1280);
        Rect frameRect=new Rect();


        public string saveFolder = "";
        public bool alwayssave = false;

        public Settings(string fldr)
        {
            //for helpfunction 
            frameMockRect = new Rect((float)(1540 - border), (float)frameBoxY, (float)(206 + border * 2), (float)(840 + border * 2));
            frameRect = mockJunk1280.r(frameMockRect);



            this.saveFolder = fldr;

            Directory.CreateDirectory(this.saveFolder + System.IO.Path.DirectorySeparatorChar);
            string[] aucfiles = Directory.GetFiles(this.saveFolder, "settings.txt");
            if (aucfiles.Contains(this.saveFolder + System.IO.Path.DirectorySeparatorChar + "settings.txt"))//File.Exists() was slower
            {
                loadSettings();
            }
            else 
            {
                saveSettings();
            }
            
            
        }


        public void saveSettings()
        {
            string txt = "alwayssave=" + this.alwayssave.ToString().ToLower();
            System.IO.File.WriteAllText(this.saveFolder + System.IO.Path.DirectorySeparatorChar + "settings.txt", txt);
        }

        public void loadSettings()
        {
            string text = System.IO.File.ReadAllText(this.saveFolder + System.IO.Path.DirectorySeparatorChar + "settings.txt");
            this.alwayssave = false;
            if (text.Contains("alwayssave=true")) { this.alwayssave = true; }
        }

        // just a helpfunction
        public Rect getButtonRect(int i)
        {
            
            Rect result = new Rect(0f, 0f, (float)Screen.height * 0.18f, (float)Screen.height * 0.055f);
            result.x = frameRect.x + (frameRect.width - result.width) / 2f;
            result.y = frameRect.y + frameRect.height - 2.8f * result.height;
            result.y += (float)i * result.height + (float)((i - 1) * Screen.height) * 0.01f;
            return result;
        }

    }
}
