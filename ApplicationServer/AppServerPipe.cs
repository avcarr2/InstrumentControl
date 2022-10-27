﻿using System;
using System.IO.Pipes; 
using System.Text; 
using Newtonsoft.Json; 
using ClientServerCommLibrary;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowServer
{
    public class AppServerPipe
    {
        public event EventHandler<EventArgs> PipeConnected;
        public event EventHandler<PipeEventArgs> PipeDataReceived;
        public NamedPipeServerStream PipeServer { get; set; }

        private IActivityCollection<IActivityContext> activityCollection;
        private SpectraActivityContext spectraActivityContext;
        private ServiceProvider serviceProvider;

        public AppServerPipe(NamedPipeServerStream pipeServer)
        {
            PipeServer = pipeServer;
            PipeDataReceived += HandleDataReceived;
            serviceProvider = new ServiceCollection().BuildServiceProvider();
        }

        public async Task StartServer(string[] startupContext)
        {
            ParseStartupContext(startupContext);
            PipeConnected += (obj, sender) =>
            {
                Console.WriteLine("Pipe client connected. Sent from event.");
            };

            var connectionResult = PipeServer.BeginWaitForConnection(Connected, null);
            // wait for the connection to occur before proceeding. 
            connectionResult.AsyncWaitHandle.WaitOne();
            connectionResult.AsyncWaitHandle.Close();
            StartReaderAsync();

            DefaultActivityRunner<IActivityContext> runner = new(serviceProvider);
            while (PipeServer.IsConnected)
            {
                await runner.RunAsync(activityCollection, spectraActivityContext);
            }
        }

        /// <summary>
        /// Method for returning data to the client
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="sender"></param>
        public void SendDataThroughPipe(object? obj, ProcessingCompletedEventArgs sender)
        {
            string temp = JsonConvert.SerializeObject(sender.ssdo);
            byte[] buffer = Encoding.UTF8.GetBytes(temp);
            byte[] length = BitConverter.GetBytes(buffer.Length);
            byte[] finalBuffer = length.Concat(buffer).ToArray();
            PipeServer.Write(finalBuffer, 0, finalBuffer.Length);
            PipeServer.WaitForPipeDrain();
        }

        /// <summary>
        /// Receives data from the client
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="eventArgs"></param>
        /// <exception cref="ArgumentException"></exception>
        private void HandleDataReceived(object? obj, PipeEventArgs eventArgs)
        {
            // convert PipeEventArgs to single scan data object
            SingleScanDataObject ssdo = eventArgs.ToSingleScanDataObject();
            if (ssdo == null) throw new ArgumentException("single scan data object is null");

            spectraActivityContext.ScanQueueManager.EnqueueScan(ssdo);

            //Workflow.ReceiveData(ssdo);
            Console.WriteLine("\n");
        }

        private void Connected(IAsyncResult ar)
        {
            OnConnection();
            PipeServer.EndWaitForConnection(ar);
        }

        private void OnConnection()
        {
            PipeConnected?.Invoke(this, EventArgs.Empty);
        }

        private void StartByteReaderAsync(Action<byte[]> packetReceived)
        {
            byte[] byteDataLength = new byte[sizeof(int)];
            PipeServer.ReadAsync(byteDataLength, 0, sizeof(int))
                .ContinueWith(t =>
                {
                    int len = t.Result;
                    int dataLength = BitConverter.ToInt32(byteDataLength, 0);
                    byte[] data = new byte[dataLength];

                    PipeServer.ReadAsync(data, 0, dataLength)
                        .ContinueWith(t2 =>
                        {
                            len = t2.Result;
                            packetReceived(data);
                            StartByteReaderAsync(packetReceived);
                        });
                });
        }

        /// <summary>
        /// Takes the args from the program and parses them into the proper fields
        /// </summary>
        /// <param name="context">args passed from god to server</param>
        /// <exception cref="ArgumentException">Thrown if deserialization results in null values</exception>
        private void ParseStartupContext(string[] context)
        {
            activityCollection = JsonConvert.DeserializeObject<IActivityCollection<IActivityContext>>(context[0]) ??
                                 throw new ArgumentException("Activity Collection not properly deserialized");
            spectraActivityContext = JsonConvert.DeserializeObject<SpectraActivityContext>(context[1]) ??
                                     throw new ArgumentException(
                                         "Spectra Activity Context not properly deserialized");
        }

        public void StartReaderAsync()
        {
            StartByteReaderAsync((b) =>
                PipeDataReceived?.Invoke(this, new PipeEventArgs(b)));
        }
    }
}
