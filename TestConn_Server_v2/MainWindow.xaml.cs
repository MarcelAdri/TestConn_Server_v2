using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Dispatching;
using System.Net.Sockets;
using System.Net;
using Windows.Security.Cryptography.Core;
using Microsoft.Web.WebView2.Core;
using System.Text;
using Microsoft.Windows.Widgets.Providers;
using Windows.ApplicationModel.Contacts;
using static MaGeneralUtilities.GeneralUtilities.GeneralUtilities; //Reguires v0.1
using TestCommData; //Requires v0.1
using Windows.Devices.Sms;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Windows.Devices.PointOfService;
using System.Reflection.Metadata;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

//This is version 0.1

namespace TestConn_Server_v2
{

    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            Verbinding(CancellationToken.None);
        }

        
        private async void Verbinding(CancellationToken cancellationTokenV)
        //Send-Receive protocol:
        //1. Anouncement
        //  Server sends byte[2]
        //      byte[0] = Acknowledge.Data2Send or Acknowledge.NoData2Send
        //  Client sends byte[2]
        //      Server resends anouncement until byte[1]=Acknowledge.CommunicationOK
        //
        //2. sending (only when there is something to send)
        //  Server sends data: byte[blocksize]
        //  Client sends acknowledgement: byte[2]
        //          byte[0] = communication status
        //          byte[1] indicates wether client had something to send
        //      Server resendse data until byte[0]=Acknowledge.CommunicationOK
        //3. receiving from client (only when there is something to receive)
        //  acknowledging wth 2 bytes
        //      Where byte[1] = Acknowledge.CommunicationOK (move on) or Acknowledge.Communication failed (repeat read)
        
        {
            TcpListener server = null;
            DataStorage dataStorage = new();
            dataStorage.Initialize();
            Acknowledge[] acknowledge = [Acknowledge.CommunicationFailed, Acknowledge.NoData2send];
            byte[] message2send = new byte[DataDefinition.blockSize];

            _ = Task.Run(() => SimConnect(CancellationToken.None));

            try
            {
                // Set the TcpListener on port 13000.
                Int32 port = 13000;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();

                Meld("Waiting 4 connection");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                using TcpClient client = await server.AcceptTcpClientAsync();
                _ = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => Meld("Connected!"));

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                // Enter the listening loop.
                while (!cancellationTokenV.IsCancellationRequested)
                {
                    int i;
                    int totalBytesRead;

                    DataBlock currentDataBlock = dataStorage.GetCurrentBlock();
                    int index = dataStorage.currentBlockIndex;
                    byte[] messageBuffer = new byte[2];
                    

                    //First: we announce a send (or not)
                    messageBuffer[1] = (byte)Acknowledge.CommunicationFailed;
                    if (currentDataBlock.Status == BlockStatus.ReceivedFromSimConnect) //something to send
                    {
                        messageBuffer[0] = (byte)Acknowledge.Data2send;
                    }
                    else
                    {
                        messageBuffer[0] = (byte)Acknowledge.NoData2send;
                    }

                    while ((Acknowledge)messageBuffer[1] != Acknowledge.CommunicationOK)
                    {
                        Meld("Sending abouncement to client");
                        stream.Write(messageBuffer, 0, messageBuffer.Length);
                        Meld("Receiving confirmation");
                        i = 0;
                        totalBytesRead = 0;
                        byte[] messageBufferIn = new byte[2];
                        while ((i = stream.Read(messageBufferIn, totalBytesRead, messageBufferIn.Length - totalBytesRead)) != 0)
                        {
                            totalBytesRead += 1;
                        }
                        messageBuffer[1] = messageBufferIn[1];
                    }

                    //Next, we do a send to client
                    acknowledge[0] = Acknowledge.CommunicationFailed;
                    if (currentDataBlock.Status == BlockStatus.ReceivedFromSimConnect) //something to send
                    {
                        if (currentDataBlock.IsLocked)
                        {
                            dataStorage.BlockReleased += (sender, args) =>
                            {
                                DataStorage.BlockLock(index); //lock block for other processes
                                dataStorage.BlockReleased -= (sender, args) => { };

                                if (currentDataBlock.Status == BlockStatus.ReceivedFromSimConnect)
                                {
                                    message2send = currentDataBlock.Data;//copy data to buffer
                                    dataStorage.SetBlockStatus(index, BlockStatus.Empty); //set status to Empty
                                    while (acknowledge[0] != Acknowledge.CommunicationOK)
                                    {
                                        Meld("Sending data to client");
                                        stream.Write(message2send, 0, message2send.Length);
                                        Meld("Receive acknowledgement");
                                        //Receive acknowledge
                                        i = 0;
                                        totalBytesRead = 0;
                                        byte[] acknowledgeBuffer = new byte[2];
                                        while ((i = stream.Read(acknowledgeBuffer, totalBytesRead, acknowledgeBuffer.Length - totalBytesRead)) != 0)
                                        {
                                            totalBytesRead += 1;
                                        }
                                        acknowledge[0] = (Acknowledge)acknowledgeBuffer[0];
                                        acknowledge[1] = (Acknowledge)acknowledgeBuffer[1];
                                    }

                                }
                                dataStorage.BlockUnlock(index); //release block for other processes
                            };
                        }
                        
                    }


                    //Next we do a read from client
                    if (acknowledge[1] == Acknowledge.Data2send)
                    {
                        messageBuffer = new byte[DataDefinition.blockSize];
                        totalBytesRead = 0;
                        byte[] reaction = new byte[2];
                        reaction[1] = (byte)Acknowledge.CommunicationFailed;
                        while ((Acknowledge)reaction[1] != Acknowledge.CommunicationOK)
                        {
                            Meld("Reading from client");
                            while ((i = stream.Read(messageBuffer, totalBytesRead, messageBuffer.Length - totalBytesRead)) != 0)
                            {
                                totalBytesRead += 1;
                            }
                            //check block is valid
                            if (DataStorage.BlockIsValid(messageBuffer))
                            {
                                //Commit data
                                //get blocknumber
                                int bn = DataDefinition.FixedDataElements.BlockNumber(messageBuffer);
                                DataBlock blockToChange = new();
                                blockToChange = DataStorage.GetBlock(bn);
                                if (blockToChange.IsLocked)
                                {
                                    dataStorage.BlockReleased += (sender, args) =>
                                    {
                                        DataStorage.BlockLock(bn); //lock block for other processes
                                        dataStorage.BlockReleased -= (sender, args) => { };
                                        dataStorage.SetBlockStatus(bn, BlockStatus.ReceivedFromClient, messageBuffer);
                                        dataStorage.BlockUnlock(bn);
                                        reaction[1] = (int)Acknowledge.CommunicationOK;
                                    };
                                }
                                else
                                {
                                    reaction[1] = (int)Acknowledge.CommunicationFailed;
                                }

                                //Send acknowledgement
                                Meld("Acknowledging receive");
                                stream.Write(reaction, 0, reaction.Length);
                            }

                        }
                        

                    }
                    
                    
                    
                }

            }
            catch (SocketException e)
            {
                Meld("SocketException: {0}" + e.ToString());
            }
            finally
            {
                server.Stop();
            }
        }

        private static void SimConnect(CancellationToken cancellationTokenSC)
        {
            //TODO: develop communication with FlightSimulator

            //Placeholder: generate random values at random intervals

            DataStorage dataStorage = new();
            DateTime startTime = DateTime.Now;
            DateTime now = new();
            var randomGenerator = new RandomGenerator();
            
            int intervalInSeconds = randomGenerator.RandomNumber(5, 25);
            string fm = "";
            Int32 fn = (Int32)0;
            
            while (!cancellationTokenSC.IsCancellationRequested)
            {
                now = DateTime.Now;
                DataBlock currentDataBlock = dataStorage.GetCurrentBlock();
                int index = dataStorage.currentBlockIndex;

                switch (currentDataBlock.Status)
                {
                    case BlockStatus.ReceivedFromClient:
                        //TODO: Send data from the client to FlightSim

                        //Placeholder: do nothing with the data, and set the status to Empty
                        if (currentDataBlock.IsLocked)
                        {
                            dataStorage.BlockReleased += (sender, args) =>
                            {
                                DataStorage.BlockLock(index);
                                dataStorage.BlockReleased -= (sender, args) => { };

                                if (currentDataBlock.Status == BlockStatus.ReceivedFromClient)
                                {
                                    dataStorage.SetBlockStatus(index, BlockStatus.Empty);

                                }
                                dataStorage.BlockUnlock(index);
                                
                            };
                        }
                        
                        break;

                    case BlockStatus.Empty:
                        //TODO: if flightsim generates new data, send these to server
                        
                        //Placeholder: send random data at random intervals to client
                        if (now.Second >= (startTime.Second + intervalInSeconds))
                        {
                            //if the block is locked, wait for release than relock it for other processes
                            if (currentDataBlock.IsLocked)
                            {
                                dataStorage.BlockReleased += (sender, args) =>
                                {
                                    DataStorage.BlockLock(index);
                                    dataStorage.BlockReleased -= (sender, args) => { };
                                };
                            }

                            //firstmessage
                            fm = randomGenerator.RandomString(10, false);
                            //firstnumber
                            fn = (Int32)randomGenerator.RandomNumber(0, Int32.MaxValue);

                            //Pack in blockstring
                            byte[] newDataBlock = new byte[DataDefinition.blockSize];
                            newDataBlock = DataDefinition.Block0DataElements.BuildBlock(fm, fn);
                            
                            //pass block to queue engine and release lock
                            dataStorage.BlockEmptied += (sender, args) =>
                            {
                                if (!cancellationTokenSC.IsCancellationRequested)
                                {
                                    dataStorage.SetBlockStatus(index, BlockStatus.ReceivedFromSimConnect, newDataBlock);
                                    dataStorage.BlockUnlock(index);
                                }
                                dataStorage.BlockEmptied -= (sender, args) => { };
                            };

                            
                        }
                        break;
                }

                dataStorage.AdvanceToNextBlock();
            }
        }
        private void Meld(string tekst)
        {
            Paragraph paragraph = new();
            Run run = new()
            {
                Text = tekst
            };

            paragraph.Inlines.Add(run);
            Melding.Blocks.Add(paragraph);
        }
        

    }
}
