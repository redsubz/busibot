using System;

using Tangine;

using Sulakore.Modules;
using Sulakore.Habbo;
using Gma.System.MouseKeyHook;
using System.Windows.Forms;
using Sulakore.Communication;
using Sulakore.Protocol;
using System.Collections.Generic;
using System.Drawing;

namespace Busi
{
    [Module("BusiBot", "Bot for nodk.")]
    [Author("rsub", ResourceName = "webpage", ResourceUrl = "http://google.com", HabboName = "busi", Hotel = HHotel.Nl)]

    public partial class MainFrm : ExtensionForm
    {
        // global keyboard hook
        private IKeyboardMouseEvents m_GlobalHook;

        // filepath for storing last set index
        private string filePath = @"" + Environment.CurrentDirectory + "\\sfile.txt";

        // headers
        private static ushort SS_EnterRoom;
        private static ushort SS_PlayerWalk;
        private static ushort CS_PlayerWalk;
        private static ushort CS_IncLoadUser;
        private static ushort CS_IncUnloadUser;
        private static ushort CS_BallChange;

        // game info
        private static bool connected = false;
        private static int habbo_ID = -1;
        private static int habbo_pos_X = -1;
        private static int habbo_pos_Y = -1;
        private static int ball_pos_X = -1;
        private static int ball_pos_Y = -1;
    
        public MainFrm()
        {
            m_GlobalHook = Hook.GlobalEvents();
            m_GlobalHook.KeyPress += GlobalHookKeyPress;

            connected = Game != null;

            InitializeComponent();

            Rectangle workingArea = Screen.GetWorkingArea(this);
            Location = new Point(workingArea.Right - Size.Width, workingArea.Bottom - Size.Height);

            TopMost = true;
            FormClosing += formClosing;

            loadFromFile();

            if (!connected)
            {
                richTextBox1.Text += "Open this module whilst connected to habbo";
            }
            else
            {
                SS_EnterRoom = GetHdr("dcb1f9aad042224029125a7625d6ee7b");
                SS_PlayerWalk = GetHdr("5dec6a7881d4a598d5b15d0e743bcdcb");

                CS_PlayerWalk = GetHdr("45d53173f4bf410c6f0d57f0fb0edca3");
                CS_BallChange = GetHdr("e76f17ac4a9202cf49c2778fc2438654");
                CS_IncLoadUser = GetHdr("9bc4789617fc483c6bf739ab2f8e8419");
                CS_IncUnloadUser = GetHdr("3593e80e70be3670dbe357f51b0c2ab4");

                Triggers.OutAttach(SS_EnterRoom, OutEnterRoom);

                Triggers.InAttach(CS_PlayerWalk, IncPlayerWalk);
                Triggers.InAttach(CS_BallChange, IncBallChange);
                Triggers.InAttach(CS_IncLoadUser, IncLoadUser);
                Triggers.InAttach(CS_IncUnloadUser, IncUnloadUser);
            }
        }

        private void GlobalHookKeyPress(object sender, KeyPressEventArgs e)
        {
            if (connected)
            {
                commandOnKeyBot(e.KeyChar);
            }
        }

        private void commandOnKeyBot(char key)
        {
            #region schieten
            if (key == '1')
            {
                if (!isTileOccupied(ball_pos_X, ball_pos_Y))
                {
                    Connection.SendToServerAsync(SS_PlayerWalk, ball_pos_X, ball_pos_Y);
                } else
                {
                    commandOnKeyBot('2');
                }
            }
            #endregion

            #region stappen
            else if (key == '2')
            {
                int distX = habbo_pos_X - ball_pos_X;
                int distY = habbo_pos_Y - ball_pos_Y;

                int coordToWalkToX = 0;
                int coordToWalkToY = 0;

                int directionX = 0;
                int directionY = 0;
                int modifier = 1;

                if (Math.Abs(distX) == Math.Abs(distY))
                {
                    // staat schuin van bal (x,y afstanden zijn gelijk)
                    if (distX > 0 && distY > 0)
                    {
                        directionX = -1;
                        directionY = -1;
                    }
                    else if (distX > 0 && distY < 0)
                    {
                        directionX = -1;
                        directionY = +1;
                    }
                    else if (distX < 0 && distY < 0)
                    {
                        directionX = +1;
                        directionY = +1;
                    }
                    else if (distX < 0 && distY > 0)
                    {
                        directionX = +1;
                        directionY = -1;
                    }
                }
                else
                {
                    // staat rechts van bal
                    if (distX > distY)
                    {
                        if (Math.Abs(distX) > Math.Abs(distY))
                        {
                            // staat rechtsonder van bal van bal
                            directionX = -1;
                            directionY = 0;
                        }
                        else
                        {
                            // staat rechtsboven van bal van bal
                            directionX = 0;
                            directionY = +1;
                        }
                    }
                    // staat links van bal
                    else
                    {
                        if (Math.Abs(distX) < Math.Abs(distY))
                        {
                            // staat linksonder van bal van bal
                            directionX = 0;
                            directionY = -1;
                        }
                        else
                        {
                            // staat linksboven van bal van bal
                            directionX = +1;
                            directionY = 0;
                        }
                    }
                }

                coordToWalkToX = ball_pos_X + (directionX * modifier);
                coordToWalkToY = ball_pos_Y + (directionY * modifier);

                if (isTileOccupied(coordToWalkToX, coordToWalkToY))
                {
                    modifier++;

                    coordToWalkToX = ball_pos_X + (directionX * modifier);
                    coordToWalkToY = ball_pos_Y + (directionY * modifier);

                    if (isTileOccupied(coordToWalkToX, coordToWalkToY))
                    {
                        modifier++;

                        coordToWalkToX = ball_pos_X + (directionX * modifier);
                        coordToWalkToY = ball_pos_Y + (directionY * modifier);
                    }
                }
                
                Connection.SendToServerAsync(SS_PlayerWalk, coordToWalkToX, coordToWalkToY);
            }
            #endregion

        }

        private Dictionary<int, Point> habboCoords = new Dictionary<int, Point>();

        private bool isTileOccupied(int x, int y)
        {
            foreach (KeyValuePair<int, Point> habboCoord in habboCoords)
            {
                Point p = habboCoord.Value;
                if (x == p.X && y == p.Y) return true;
            }

            return false;
        }

        private void IncPlayerWalk(DataInterceptedEventArgs e)
        {
            HMessage packet = e.Packet;
            int packetSize = packet.ReadInteger();
            
            for (int i = 0; i < packetSize; i++)
            {
                int index = packet.ReadInteger();

                int oldX = packet.ReadInteger(); //old_x
                int oldY = packet.ReadInteger(); //old_y
                packet.ReadString();
                packet.ReadInteger();
                packet.ReadInteger();

                string pos_string = packet.ReadString();

                string[] parts = pos_string.Split(new string[] { "/mv " }, StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string[] coords = parts[1].Split(new string[] { "," }, StringSplitOptions.None);

                    int coordX = Convert.ToInt32(coords[0]);
                    int coordY = Convert.ToInt32(coords[1]);

                    habboCoords[index] = new Point(coordX, coordY);

                    if (habbo_ID == index)
                    {
                        habbo_pos_X = coordX;
                        habbo_pos_Y = coordY;

                        label5.Text = habbo_pos_X + "";
                        label6.Text = habbo_pos_Y + "";
                    }

                }
            }


        }

        private void IncBallChange(DataInterceptedEventArgs e)
        {
            byte[] bytes = e.Packet.ToBytes();

            ball_pos_X = bytes[17];
            ball_pos_Y = bytes[21];

            label7.Text = ball_pos_X + "";
            label8.Text = ball_pos_Y + "";
        }

        private void OutEnterRoom(DataInterceptedEventArgs obj)
        {
            habboCoords = new Dictionary<int, Point>();
            listBox1.Items.Clear();
        }

        private void IncLoadUser(DataInterceptedEventArgs e)
        {
            int entityCount = e.Packet.ReadInteger();

            for (int i = 0; i < entityCount; i++)
            {
                e.Packet.ReadInteger();
                string name = e.Packet.ReadString();
                e.Packet.ReadString();
                e.Packet.ReadString();
                int index = e.Packet.ReadInteger();
                int x = e.Packet.ReadInteger();
                int y = e.Packet.ReadInteger();

                e.Packet.ReadString();

                e.Packet.ReadInteger();
                int type = e.Packet.ReadInteger();

                switch (type)
                {
                    case 1:
                        {
                            e.Packet.ReadString();
                            e.Packet.ReadInteger();
                            e.Packet.ReadInteger();
                            e.Packet.ReadString();
                            e.Packet.ReadString();
                            e.Packet.ReadInteger();
                            e.Packet.ReadBoolean();
                            break;
                        }
                    case 2:
                        {
                            e.Packet.ReadInteger();
                            e.Packet.ReadInteger();
                            e.Packet.ReadString();
                            e.Packet.ReadInteger();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadBoolean();
                            e.Packet.ReadInteger();
                            e.Packet.ReadString();
                            break;
                        }
                    case 4:
                        {
                            e.Packet.ReadString();
                            e.Packet.ReadInteger();
                            e.Packet.ReadString();
                            for (int j = e.Packet.ReadInteger(); j > 0; j--)
                                e.Packet.ReadShort();
                            break;
                        }
                }

                habboCoords.Add(index, new Point(x, y));
                listBox1.Items.Add(name + "`" + index);
            }

        }

        private void IncUnloadUser(DataInterceptedEventArgs e)
        {
            string packetString = e.Packet.ToString();

            string last = packetString.Substring(packetString.LastIndexOf(']') + 1);

            int index = Convert.ToInt32(last);

            if (habboCoords.ContainsKey(index))
            {
                habboCoords.Remove(index);
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
            {
                string s = listBox1.SelectedItem.ToString();
                string[] r = s.Split('`');

                habbo_ID = Convert.ToInt32(r[1]);

                label2.Text = habbo_ID + "";
            }
        }

        private void debug(string s)
        {
            richTextBox1.Text = s + "\n" + richTextBox1.Text;
        }

        //clean debugger
        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox1.Text = "";
        }

        private void formClosing(object sender, EventArgs e)
        {
            m_GlobalHook.KeyPress -= GlobalHookKeyPress;
            m_GlobalHook.Dispose();

            System.IO.File.WriteAllLines(filePath, new string[]{
                habbo_ID + ""
            });
        }

        private void loadFromFile()
        {
            if (System.IO.File.Exists(filePath))
            {
                string[] lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 1) return;

                habbo_ID = Convert.ToInt32(lines[0]);
                label2.Text = habbo_ID + "";
            }
        }

        public ushort GetHdr(string hash) =>
            Game.GetMessageHeader(Game.GetMessages(hash)[0]);

    }
}

/*
//penalty
                for (int i = 0; i < packetSize; i++)
            {
                int index = packet.ReadInteger();

                int oldX = packet.ReadInteger(); //old_x
                int oldY = packet.ReadInteger(); //old_y
                packet.ReadString();
                packet.ReadInteger();
                packet.ReadInteger();

                string pos_string = packet.ReadString();

                string[] parts = pos_string.Split(new string[] { "/mv " }, StringSplitOptions.None);

                if (parts.Length == 2)
                {
                    string[] coords = parts[1].Split(new string[] { "," }, StringSplitOptions.None);

                    int coordX = Convert.ToInt32(coords[0]);
                    int coordY = Convert.ToInt32(coords[1]);

                    habboCoords[index] = new Point(coordX, coordY);

                    // penalty killer
                    if (checkBox1.Checked)
                    {
                        int xStart = habbo_pos_X - 1;
                        int yStart = habbo_pos_Y;
                        bool standRight = checkBox2.Checked;
                        
                        if (standRight)
                        {
                            if (coordY == yStart + 2 && coordX == xStart)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 2);
                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 2, yStart);
                            }
                            else if (coordY == yStart + 2 && coordX == xStart + 1)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 1);

                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 1, yStart);
                            }
                            else if (coordY == yStart + 2 && coordX == xStart + 2)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 0);
                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 0, yStart);
                            }
                        } else
                        {
                            if (coordY == yStart - 2 && coordX == xStart)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 2);
                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 2, yStart);
                            }
                            else if (coordY == yStart - 2 && coordX == xStart + 1)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 1);

                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 1, yStart);
                            }
                            else if (coordY == yStart - 2 && coordX == xStart + 2)
                            {
                                debug("clicked on " + yStart + ":" + xStart + 0);
                                Connection.SendToServerAsync(SS_PlayerWalk, xStart + 0, yStart);
                            }
                        }
                    }


                    if (habbo_ID == index)
                    {
                        habbo_pos_X = coordX;
                        habbo_pos_Y = coordY;

                        label5.Text = habbo_pos_X + "";
                        label6.Text = habbo_pos_Y + "";
                    }

                }
            }

    */
