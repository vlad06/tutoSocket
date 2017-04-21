using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace tutoSocket
{
    class forwardToAll
    {
        public ArrayList acceptList = new ArrayList();
        public Hashtable MatchList = new Hashtable();

        public forwardToAll() { }//constructeur

        public void sendMsg (byte[] msg)
        {
            for(int i = 0; i < acceptList.Count; i++)
            {
                if (((Socket)acceptList[i]).Connected)
                {
                    try
                    {
                        int bytesSent = ((Socket)acceptList[i]).Send(msg, msg.Length, SocketFlags.None);
                    }
                    catch
                    {
                        Console.Write(((Socket)acceptList[i]).GetHashCode() + " déconnecté");
                    }
                }
                else
                {
                    acceptList.Remove((Socket)acceptList[i]);
                    i--;
                }
            }
        }
    }
}
