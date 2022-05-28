using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data;
using MySQL;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Connections.Features;

namespace SignalRMySQLQueue.Hubs
{
    public class DbQueueConfig
    {
        public static int ThreadCount { get; set; }
        public static int MaxConnections { get; set; }
    }


    public class Client : Object
    {
        public IHubCallerClients Clients { get; set; }
        public Session Session { get; set; }
    }




    public class DbQueue : Hub
    {
        static string authtoken = "ab53-0ksl-ahfa-dnsh-34rs-92ds";
        static int thread_count = DbQueueConfig.ThreadCount;
        static int max_connections = DbQueueConfig.MaxConnections;
        static ThreadsManager.Functions tm = new ThreadsManager.Functions(thread_count);


        static List<Session> ConnectedSessions = new List<Session>();
        static List<Queue> Queue = new List<Queue>();
        static long idleTimeout = 5000;


        #region Connections
        public override async Task OnConnectedAsync()
        {
            var feature = Context.Features.Get<IConnectionHeartbeatFeature>();
            feature.OnHeartbeat(context => {
                for (int i = ConnectedSessions.Count - 1; i >= 0; i--)
                {
                    if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - ConnectedSessions[i].Time > idleTimeout && ConnectedSessions[i].Finished && ConnectedSessions[i].Disposable)
                    {
                        ConnectedSessions[i].Context.Abort();
                        ConnectedSessions.Remove(ConnectedSessions[i]);
                    }
                }

            }, Context);
        }


        public async Task Connect(string SessionId, string token)
        {
            if (ConnectedSessions.Count() >= max_connections || token != authtoken)
            {
                await Clients.Caller.SendAsync("Refused", "Connection Refused", "Max Connections Exausted");
                await base.OnDisconnectedAsync(null);
                Context.Abort();
                return;
            }

            try
            {
                bool exists = false;
                var id = Context.ConnectionId;

                if (ConnectedSessions.Count(x => x.SessionId == SessionId) == 0)
                {
                    lock (ConnectedSessions)
                    {
                        ConnectedSessions.Add(new Session() { ConnectionId = id, SessionId = SessionId, Time = DateTimeOffset.Now.ToUnixTimeMilliseconds(), Context = Context, Finished = false, Disposable = true });
                    }
                }
                else
                {
                    exists = true;
                }


                if (exists)
                {
                    Session CurrentSession = ConnectedSessions.Where(u => u.SessionId == SessionId).FirstOrDefault();
                    CurrentSession.ConnectionId = id;
                    CurrentSession.Time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    CurrentSession.Context = Context;
                }

                await Clients.Caller.SendAsync("Accepted", "Connection Accepted", id);

            }
            catch (Exception err)
            {
                await Clients.Caller.SendAsync("ConnectionError", "Connection Error", err.Message);
            }
        }


        public async Task Disconnect(string SessionId = "")
        {
            Session item = SessionId == "" ? ConnectedSessions.Where(x => x.ConnectionId == Context.ConnectionId).FirstOrDefault() : ConnectedSessions.Where(u => u.SessionId == SessionId).FirstOrDefault();
            if (item != null)
            {
                try
                {
                    await Clients.All.SendAsync("Disconnect", item.ConnectionId, item.SessionId);
                    ConnectedSessions.Remove(item);
                    await base.OnDisconnectedAsync(new Exception());
                }
                catch (Exception err) { }
            }

        }


        public override async Task OnDisconnectedAsync(Exception exception)
        {
            Session item = ConnectedSessions.Where(x => x.ConnectionId == Context.ConnectionId).FirstOrDefault();
            if (item != null)
            {
                await Clients.All.SendAsync("Disconnect", item.ConnectionId, item.SessionId);
                lock (ConnectedSessions)
                {
                    ConnectedSessions.Remove(item);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        #endregion Connections


        #region Queues
        public async Task Enqueue(string hash, string query, string[] parms, int priority = 0)
        {
            Session CurrentSession = ConnectedSessions.Where(x => x.ConnectionId == Context.ConnectionId).FirstOrDefault();
            CurrentSession.Time = DateTimeOffset.Now.ToUnixTimeMilliseconds();


            Queue.Add(new Queue() { hash = hash, query = query, parms = parms, priority = priority });
            tm.AddFunction(Proc, new Client(){ Clients = Clients, Session = CurrentSession });
        }


        public async Task Ping(string hash)
        {
            await Clients.Caller.SendAsync("Accepted", "Pinged", Context.ConnectionId);
        }


        public async Task Abort()
        {
            Context.Abort();
        }


        public async Task EnqueueDirect(string hash, string query)
        {
            try
            {
                Session CurrentSession = ConnectedSessions.Where(x => x.ConnectionId == Context.ConnectionId).FirstOrDefault();
                CurrentSession.Finished = false;
                DataSet ds = Data.Query(query, new string[] { });
                string o = JsonConvert.SerializeObject(ds);
                await Clients.Caller.SendAsync("QuerySuccess", hash, o);
                CurrentSession.Finished = true;
            }
            catch (Exception err)
            {
                await Clients.Caller.SendAsync("QueryError", hash, err.Message);
            }

        }

        public bool Proc(object args)
        {
            Queue _queue = null;

            try
            {
                if (Queue.Count() == 0)
                {

                }
                else
                {
                    lock (Queue)
                    {
                        _queue = Queue[0];
                        Queue.RemoveAt(0);
                    }

                    if (_queue != null)
                    {
                        ((Client)args).Session.Finished = false;
                        DataSet ds = Data.Query(_queue.query, _queue.parms);
                        string o = JsonConvert.SerializeObject(ds);
                        ((Client)args).Clients.Caller.SendAsync("QuerySuccess", _queue.hash, o);
                        ((Client)args).Session.Finished = true;
                    }
                }
            }
            catch (Exception err)
            {
                ((Client)args).Clients.Caller.SendAsync("QueryError", _queue.hash, err.Message);
                ((Client)args).Session.Finished = true;
            }


            return true;
        }
        #endregion Queues
    }



    public class Session
    {
        public string ConnectionId { get; set; }
        public string SessionId { get; set; }
        public long Time { get; set; }
        public HubCallerContext Context { get; set; }
        public bool Disposable { get; set; }
        public bool Finished { get; set; }
    }


    public class Queue
    {
        public string hash { get; set; }
        public string query { get; set; }
        public string[] parms { get; set; }
        public int priority { get; set; }
    }
}
