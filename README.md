# SignalRMySQLQueue

Create appsettings.json in project root

``` 
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnectionString": "server=MYSQLSERVER1; user id=USER; password=PASSWORD; port=3306; database=DATABASE; Allow Zero Datetime=True;Allow User Variables=True;SslMode=none;Pooling=false;",
    "CacheConnectionString": "server=MYSQLSERVER2; user id=USER; password=PASSWORD; port=3306; database=DATABASE; Allow Zero Datetime=True;Allow User Variables=True;SslMode=none;Pooling=false;",

    "DbQueueThreadCount": "100",
    "DbQueueMaxConnections": "100"
  }
}
``` 
