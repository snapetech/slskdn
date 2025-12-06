/**
 * Bug Reproduction Test: async void event handler
 * 
 * Bug #31: RoomService.cs Client_LoggedIn is async void
 * 
 * PROBLEM: In C#, async void methods don't propagate exceptions
 * to the caller. If an unhandled exception occurs, it crashes
 * the entire process.
 * 
 * This is a conceptual demonstration - run as part of a .NET test project.
 * 
 * UPSTREAM CODE (vulnerable):
 * private async void Client_LoggedIn(object sender, EventArgs e)
 * {
 *     await DoSomethingAsync(); // If this throws, process crashes!
 * }
 * 
 * OUR FIX:
 * private async void Client_LoggedIn(object sender, EventArgs e)
 * {
 *     try
 *     {
 *         await DoSomethingAsync();
 *     }
 *     catch (Exception ex)
 *     {
 *         Log.Error(ex, "Error in Client_LoggedIn");
 *         // Process continues running
 *     }
 * }
 * 
 * To demonstrate this programmatically, we would need to:
 * 1. Trigger a login event
 * 2. Have the rooms/shares API fail
 * 3. Watch the process crash (upstream) vs continue (ours)
 */

using System;
using System.Threading.Tasks;

namespace BugTest
{
    public class AsyncVoidBugDemo
    {
        // Simulates upstream vulnerable code
        public async void UpstreamHandler(object sender, EventArgs e)
        {
            Console.WriteLine("Upstream: Starting handler...");
            await Task.Delay(100);
            throw new Exception("Simulated API failure!");
            // Process crashes here - exception escapes to ThreadPool
        }

        // Simulates our fixed code  
        public async void FixedHandler(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Fixed: Starting handler...");
                await Task.Delay(100);
                throw new Exception("Simulated API failure!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fixed: Caught exception safely: {ex.Message}");
                // Process continues running
            }
        }

        public static async Task Main(string[] args)
        {
            var demo = new AsyncVoidBugDemo();
            
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("BUG REPRODUCTION: async void exception handling");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();

            // Test fixed version first (safe)
            Console.WriteLine("Testing FIXED handler (with try-catch):");
            var fixedEvent = new EventHandler((s, e) => demo.FixedHandler(s, e));
            fixedEvent(null, EventArgs.Empty);
            await Task.Delay(500);
            Console.WriteLine("✓ Process still running after fixed handler\n");

            // Note: Testing upstream would crash the process
            // Uncomment to see the crash:
            // Console.WriteLine("Testing UPSTREAM handler (no try-catch):");
            // var upstreamEvent = new EventHandler((s, e) => demo.UpstreamHandler(s, e));
            // upstreamEvent(null, EventArgs.Empty);
            // await Task.Delay(500);
            // Console.WriteLine("This line won't print - process crashed!");

            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("SUMMARY");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("Upstream: Would crash process (async void + unhandled exception)");
            Console.WriteLine("SLSKDN:   Catches exception safely, process continues");
        }
    }
}

