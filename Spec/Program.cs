using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.SignalR.Client;
using MySQL;
using Newtonsoft.Json;

namespace Spec
{
    class Program
    {
        static void Main(string[] args)
        {
            DataSet ds = LazyQuery("SELECT * FROM users LIMIT 10", new string[] { });

            foreach (DataRow row in ds.Tables[0].Rows)
            {
                Console.WriteLine("A " + row[0].ToString());
            }

            Thread.Sleep(2000);

            DataSet ds1 = LazyQuery("SELECT COUNT(*) FROM users", new string[] { });
            foreach (DataRow row1 in ds1.Tables[0].Rows)
            {
                Console.WriteLine("B " + row1[0].ToString());
            }

            Console.ReadKey();
        }


        static DataSet LazyQuery(string query, string[] parms)
        {
            DataSet _ds = null;

            Query(query, parms, "https://dbhub.lifequery.org/dbqueue", Guid.NewGuid().ToString(), ((DataSet ds) =>
            {
                _ds = ds;
                return true;
            }));

            while (_ds == null) { }

            return _ds;
        }


        static HubConnection connection = null;
        static Dictionary<string, Func<DataSet, bool>> memory = new Dictionary<string, Func<DataSet, bool>>();


        static void Query(string query, string[] parms, string dbQueueServer, string hash, Func<DataSet, bool> func)
        {
            Console.WriteLine(hash);
            memory.Add(hash, func);

            if (connection == null)
            {
                Console.WriteLine("Connection Initlized");
                connection = new HubConnectionBuilder().WithUrl(dbQueueServer).Build();
                connection.Closed += async (error) =>
                {
                    await Task.Delay(1);
                };

                connection.On<string, string>("Refused", (descrition, message) =>
                {
                    Console.WriteLine($"Refuse: {descrition} / {message}");
                });

                connection.On<string, string>("Accepted", (descrition, message) =>
                {
                    Console.WriteLine($"Accepted: {descrition} / {message}");
                });

                connection.On<string, string>("Disconected", (descrition, message) =>
                {
                    Console.WriteLine($"Disconected: {descrition} / {message}");
                });

                connection.On<string, string>("Heartbeat", (descrition, message) =>
                {
                    Console.WriteLine($"Heartbeat: {descrition} / {message}");
                });


                connection.On<string, string>("QuerySuccess", (_hash, result) =>
                {
                    DataSet ds = JsonConvert.DeserializeObject<DataSet>(result);
                    Console.WriteLine($"QuerySuccess: {_hash} / {result}");

                    memory[_hash](ds);
                    memory.Remove(_hash);
                });

                connection.On<string, string>("QueryError", (descrition, message) =>
                {
                    Console.WriteLine($"QueryError: {descrition} / {message}");
                });
            }


            try
            {
                connection.StartAsync();
                connection.InvokeAsync("Connect", Guid.NewGuid());
            }
            catch (Exception err) { }

            //connection.InvokeAsync("Ping", hash);
            //connection.InvokeAsync("EnqueueDirect", hash, query);
            connection.InvokeAsync("Enqueue", hash, query, parms, 0);
        }
    }
}
