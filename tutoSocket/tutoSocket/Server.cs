﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace tutoSocket
{
    class Server : forwardToAll
    {
        //http://stephaneey.developpez.com/tutoriel/dotnet/sockets/
        //http://csharp.net-informations.com/communications/csharp-socket-programming.htm
        //https://www.codeproject.com/Articles/463947/Working-with-Sockets-in-Csharp
        ArrayList readList = new ArrayList();   //liste utilisée par socket.select
        string msgString = null;    //contiendra le message envoyé aux autres clients
        string msgDisconnected = null; //Notification connexion/déconnexion
        byte[] msg; //Message sous forme de bytes pour socket.send et socket.receive
        public bool useLogging = false; //permet de looger le processing ds un fichier log
        public bool readLock = false; //flag aidant à la synchronisation
        private string rtfMsgEncStart = "\pard\cf1\b0\f1 "; //code RTF
        private string rtfMsgContent = "\cf2 "; //code RTF
        private string rtfConnMsgStart = "\pard\qc\b\f0\fs20 "; //code RTF

        public void Start()
        {
            //réception de l'adresse ip locale
            IPHostEntry ipHostEntry = Dns.Resolve(Dns.GetHostName());
            IPAddress ipAddress = ipHostEntry.AddressList[0];
            Console.WriteLine("IP=" + ipAddress.ToString());
            Socket currentClient = null;
            //création de la socket
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                serverSocket.Bind(new IPEndPoint(ipAddress, 8000));//on lie la socket au point de communication
                serverSocket.Listen(10);//on la positionne en mode "écoute"
                //démarrage du thread avant la première connexion client
                Thread getReadClients = new Thread(new ThreadStart(getRead));
                getReadClients.Start();
                //démarrage du thread vérifiant l'état des connexions clientes
                Thread pingPongThread = new Thread(new ThreadStart(checkIfStillConnected));
                pingPongThread.Start();
                //Boucle infinie : 
                while (true)
                {
                    Console.WriteLine("Attente d'une nouvelle connexion...");
                    //l'exécution du thread courant est bloquée jusqu'à ce qu'un nouveau client se connecte
                    currentClient = serverSocket.Accept();
                    Console.WriteLine("Nouveau client:" + currentClient.GetHashCode());
                    //stockage de la ressource dans l'arraylist acceptlist
                    acceptList.Add(currentClient);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
            }
        }
        //Méthode permettant de générer du logging ds un fichier log selon que le membre useLogging soit à true ou false
        private void logging(string message)
        {
            using (StreamWriter sw = File.AppendText("chatServer.log"))
            {
                sw.WriteLine(DateTime.Now + ": " + message);
            }
        }
        //Méthode démarrant l'écriture du message reçu par un client vers tous les autres clients
        private void writeToAll()
        {
            base.sendMsg(msg);
        }
        private void infoToAll()
        {
            base.sendMsg(msgDisconnected);
        }

        private void checkIfStillConnected()
        {
            /* Etant donné que la propriété .Connected d'une socket n'est pas mise à jour lors de la déconnexion d'un 
             * client sans que l'on ait prélablement essayé de lire ou d'écrire sur cette socket, cette méthode 
             * parvient à déterminer si une socket cliente s'est déconnectée grce à la méthode poll. On effectue 
             * un poll en lecture sur la socket, si le poll retourne vrai et que le nombre de bytes disponible 
             * est 0, il s'agit d'une connexion terminée
             */
            while (true)
            {
                for (int i = 0; i < acceptList.Count; i++)
                {
                    if (((Socket)acceptList[i]).Poll(10, SelectMode.SelectRead) && ((Socket)acceptList[i]).Available == 0)
                    {
                        if (!readLock)
                        {
                            Console.WriteLine("Client " + ((Socket)acceptList[i]).GetHashCode() + " déconnecté");
                            removeNickname(((Socket)acceptList[i]));
                            ((Socket)acceptList[i]).Close();
                            acceptList.Remove(((Socket)acceptList[i]));
                            i--;
                        }
                    }
                }
                Thread.Sleep(5);
            }
        }

        private bool checkNick(string nick, Socket Resource)
        {
            if (MatchList.Contains(nick))
            {
                ((Socket)acceptList[acceptList.IndexOf(Resource)]).Shutdown(SocketShutdown.Both);
                ((Socket)acceptList[acceptList.IndexOf(Resource)]).Close();
                acceptList.Remove(Resource);
                Console.WriteLine("Pseudo déjà pris");
                return false;
            }
            else
            {
                MatchList.Add(Resource, nick);
                getConnected();
            }
            return true;
        }

        private void removeNick(Socket Resource)
        {
            Console.Write("DECONNEXION DE :" + MatchList[Resource]);
            msgDisconnected = rtfConnMsgStart + ((string)MatchList[Resource]).Trim() + " vient de se déconnecter!/par";
            Thread discInfoToAll = new Thread(new ThreadStart(infoToAll));
            discInfoToAll.Start();
            discInfoToAll.Join();
            MatchList.Remove(Resource);
        }
        /// <summary>
        /// Cette méthode est exécutée dans un thread à part, elle lit en permanance l'état des sockets connectées et vérifie
        /// si celles-ci tentent d'envoyer qqch au serveur. Si c'est le cas, elle réceptionne les paquets et appelle forwardToAll
        /// pour renvoyer ces paquets vers les autres clients.
        /// </summary>
        private void getRead()
        {
            while (true)
            {
                readList.Clear();
                for (int i = 0; i < acceptList.Count; i++)
                {
                    readList.Add((Socket)acceptList[i]);
                }
                if (readList.Count > 0)
                {
                    Socket.Select(readList, null, null, 1000);
                    for (int i = 0; i < readList.Count; i++)
                    {
                        if (((Socket)readList[i]).Available > 0)
                        {
                            readLock = true;
                            int packetsReceived = 0;
                            long sequence = 0;
                            string Nick = null;
                            string formattedMsg = null;
                            while (((Socket)readList[i]).Available > 0)
                            {
                                msg = new byte[((Socket)readList[i]).Available];
                                ((Socket)readList[i]).Receive(msg, msg.Length, SocketFlags.None);
                                msgString = System.Text.Encoding.UTF8.GetString(msg);
                                if (packetsReceived == 0)
                                {
                                    string seq = msgString.Substring(0, 6);
                                    try
                                    {
                                        sequence = Convert.ToInt64(seq);
                                        Nick = msgString.Substring(6, 15);
                                        formattedMsg = rtfMsgEncStart + Nick.Trim() + " a écrit : " + rtfMsgContent +
                                            msgString.Substring(20, (msgString.Length - 20)) + "/par";
                                    }
                                    catch
                                    {
                                        Console.Write("Message non conforme");      //uniquement dans le cas ou un client développé par qqun
                                        acceptList.Remove(((Socket)readList[i]));   //d'autre tente de se connecter au serveur.
                                        break;
                                    }
                                }
                                else
                                {
                                    formattedMsg = rtfMsgContent + msgString + "/par";
                                }
                                msg = System.Text.Encoding.UTF8.GetBytes(formattedMsg);
                                if (sequence == 1)
                                {
                                    if (!checkNick(Nick, ((Socket)readList[i])))
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        string rtfMessage = rtfConnMsgStart + Nick.Trim() + " vient de se connecter\par";
                                        msg = System.Text.Encoding.UTF8.GetBytes(rtfMessage);
                                    }
                                }
                                if (useLogging)
                                {
                                    logging(formattedMsg);
                                }
                                //démarrage du thread renvoyant le message à tous les clients
                                Thread forwardingThread = new Thread(new ThreadStart(writeToAll));
                                forwardingThread.Start();
                                forwardingThread.Join();
                                packetsReceived++;
                            }
                            readLock = false;
                        }
                    }
                }
                Thread.Sleep(10);
            }
        }

        


    } }
