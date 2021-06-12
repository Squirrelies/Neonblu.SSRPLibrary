using System;
using System.Data;
using System.Threading.Tasks;

namespace SSRPClient.Tester
{
    public static class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("*** Scanning...");
            using (SSRPClient ssrpClient = new SSRPClient())
            {
                // Retrieve record-style response.
                await foreach (SqlInstance serverInfo in ssrpClient.GetServersAsync())
                    Console.WriteLine(serverInfo.ToString());

                // Wait 5s before hitting the server browsers again. They don't seem to like fast consecutive calls.
                await Task.Delay(5000);

                // Retrieve DataTable-style response.
                DataTable sources = await ssrpClient.GetDataSourcesAsync();
            }
            Console.WriteLine("*** Done!");
            Console.ReadKey();
        }
    }
}
