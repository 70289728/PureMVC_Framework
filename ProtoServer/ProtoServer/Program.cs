using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ProtoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize data store (JSON file based)
            IDataStore dataStore = new JsonDataStore();

            var server = new TCPServer(IPAddress.Any, 5060, dataStore);

            // Start GM console immediately (runs on thread pool)
            _ = Task.Run(() => RunGmConsoleAsync(server));

            await server.StartAsync();

            Console.ReadLine();
        }

        private static async void RunGmConsoleAsync(TCPServer server)
        {
            await Task.Yield(); // let server start first
            Console.WriteLine("[GM] Console ready. Commands: addannounce <title> <content> <priority>");
            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("addannounce "))
                {
                    var parts = line.Substring(12).Split(new[] { ' ' }, 3);
                    if (parts.Length >= 2)
                    {
                        var title = parts[0];
                        var content = parts[1];
                        var priority = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
                        server.AddAnnounce(title, content, priority);
                    }
                }
                else if (line.StartsWith("delannounce "))
                {
                    if (int.TryParse(line.Substring(12), out var id))
                        server.RemoveAnnounce(id);
                }
                else if (line == "listannounce")
                {
                    foreach (var a in server.Announces)
                        Console.WriteLine($"  [{a.Id}] {a.Title} (priority={a.Priority})");
                }
            }
        }
    }
}