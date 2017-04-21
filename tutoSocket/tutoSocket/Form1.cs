using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace tutoSocket
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            //EXEMPLE DE CREATION D'UN SOCKET
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //COTE SERVEUR :
            int tcpPort = 1212;

            IPEndPoint iep = new IPEndPoint(IPAddress.Parse("192.168.0.4"), tcpPort);
            sock.Bind(iep);
        }
    }
}
