using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exuarch.Core;
using Microsoft.JSInterop;

namespace Exuarch.Web
{
    // What people do in the app, counted anonymously (see wwwroot/js/analytics.js). Only the events below are sent, and
    // only with the values listed: names of built in machines, tabs and handbook pages, and numbers in coarse buckets.
    // Umami bills every property as an event of its own, so each event carries as few as it needs.
    // Nothing a person writes or names is ever sent: a machine of their own is reported as "own", and programs,
    // microcode and machine definitions never leave the browser.
    public class Analytics
    {
        private readonly IJSRuntime js;
        private readonly HashSet<string> once = new HashSet<string>();

        public Analytics(IJSRuntime js)
        {
            this.js = js;
        }

        // A built in package by its name, anything else as "own".
        public static string MachineName(string name) => name != null && Workspace.IsBuiltIn(name) ? name : "own";

        public Task Tab(string tab) => Send("tab", ("tab", tab));
        public Task Machine(string name) => Send("machine", ("machine", MachineName(name)));
        public Task Page((string Kind, string Target) page)
        {
            // Only pages the handbook has: a guide by its id, or a device type's reference page.
            var known = page.Kind == "guide" ? Guides.Find(page.Target) != null : page.Kind == "reference" && DeviceReference.Exists(page.Target);
            return known ? Send("handbook", ("page", $"{page.Kind}/{page.Target}")) : Task.CompletedTask;
        }
        public Task Drawer(string section) => Send("drawer", ("section", section));
        public Task NewMachine(string start) => Send("new machine", ("start", start is "empty" or "copy" ? start : "minimal"));
        public Task Import() => Send("import");
        public Task Export(string name) => Send("export", ("machine", MachineName(name)));
        public Task Reset(string name) => Send("reset", ("machine", MachineName(name)));
        public Task AutoLayout() => Send("auto layout");
        public Task Run(string machine, bool max) => Send("run", ("machine", MachineName(machine)), ("speed", max ? "max" : "set"));

        // Stepping by hand is counted once per machine and visit, so a hundred presses of Tick are one event.
        public Task Step(string machine, string how)
        {
            var key = $"{how}:{machine}";
            return once.Add(key) ? Send("step", ("machine", MachineName(machine)), ("how", how)) : Task.CompletedTask;
        }

        // How fast the simulator runs flat out, measured over a run at max speed of a few seconds or more.
        public Task Speed(string machine, double ticksPerSecond) =>
            Send("speed", ("machine", MachineName(machine)), ("ticks per second", SpeedBucket(ticksPerSecond)));

        public async Task Loaded()
        {
            double seconds;
            try { seconds = await js.InvokeAsync<double>("exuarchAnalytics.loadSeconds"); }
            catch (JSException) { return; }
            if (seconds >= 0) await Send("loaded", ("seconds", LoadBucket(seconds)));
        }

        public static string SpeedBucket(double ticksPerSecond) => ticksPerSecond switch
        {
            < 1_000 => "under 1k",
            < 10_000 => "1k to 10k",
            < 100_000 => "10k to 100k",
            < 1_000_000 => "100k to 1M",
            < 10_000_000 => "1M to 10M",
            _ => "10M and more",
        };

        public static string LoadBucket(double seconds) => seconds switch
        {
            < 1 => "under 1",
            < 2 => "1 to 2",
            < 4 => "2 to 4",
            < 8 => "4 to 8",
            _ => "8 and more",
        };

        private async Task Send(string name, params (string Key, string Value)[] data)
        {
            try
            {
                var properties = data.Length == 0 ? null : data.ToDictionary(d => d.Key, d => d.Value);
                await js.InvokeVoidAsync("exuarchAnalytics.track", name, properties);
            }
            catch (JSException) { }
            catch (InvalidOperationException) { }
            catch (TaskCanceledException) { }
        }
    }
}
