using System.Diagnostics;
using System.Text;

namespace Bot.Handlers;

public class MnamerHandler
{
    public string MovieFormat { get; }
    public string MovieDirectoryFormat { get; }
    public string EpisodeFormat { get; }
    public string EpisodeDirectoryFormat { get; }
    
    public string Language { get; }

    public MnamerHandler(DirectoryHandler dirHandler)
    {
        MovieFormat = Environment.GetEnvironmentVariable("MOVIE_FORMAT") ??
                      "{name} ({year}){extension}";
        EpisodeFormat = Environment.GetEnvironmentVariable("EPISODES_FORMAT") ??
                        "{series} S{season:02}E{episode:02}{extension}";

        MovieDirectoryFormat = Environment.GetEnvironmentVariable("MOVIE_DIRECTORY") ??
                         $"{dirHandler.MoviesDirectory}/{{name}} ({{year}}) [tmdbid-{{id_tmdb}}]";
        EpisodeDirectoryFormat = Environment.GetEnvironmentVariable("EPISODE_DIRECTORY") ??
                           $"{dirHandler.ShowsDirectory}/{{series}} [tvdbid-{{id_tvdb}}]/Season {{season:02}}";
        
        Language = Environment.GetEnvironmentVariable("LANGUAGE") ?? "en";
        
        Log.Info($"Mnamer set with:\nMovieFormat: {MovieFormat}\nMovieDirectoryFormat: {MovieDirectoryFormat}\nEpisodeFormat: {EpisodeFormat}\nEpisodeDirectoryFormat: {EpisodeDirectoryFormat}\nLanguage: {Language}");
    }
    
    public string GetConfigurationSummary()
    {
        return
            $"🎞 *Mnamer Configuration:*\n" +
            $"Movie format: `{MovieFormat}`\n" +
            $"Episode format: `{EpisodeFormat}`\n" +
            $"Movie directory: `{MovieDirectoryFormat}`\n" +
            $"Episode directory: `{EpisodeDirectoryFormat}`\n" +
            $"Language: `{Language}`";
    }


    public async Task<string> ExecuteMnamer(string arguments)
    {
        Log.Info($"Executing maner with arguments: {arguments}");
        
        var psi = new ProcessStartInfo
        {
            FileName = "mnamer",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };
        process.Start();

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine("[ERR] " + e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        
        Log.Info("Mnamer Result: " + output);
        
        return output.ToString();
    }
}