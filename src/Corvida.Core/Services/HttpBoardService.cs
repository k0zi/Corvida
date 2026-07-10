using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Corvida.Models;

namespace Corvida.Services;

public class HttpBoardService(IHttpClientFactory factory, ISettingsService settings) : IBoardService
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5000").TrimEnd('/');

    public async Task<List<Board>> GetBoardsAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<List<Board>>($"{Base}/api/boards") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task<Board> CreateBoardAsync(string name)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync($"{Base}/api/boards", new { Name = name });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<Board>())!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task SaveBoardAsync(Board board)
    {
        try
        {
            var resp = await Client.PutAsJsonAsync($"{Base}/api/boards/{board.Id}", board);
            resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }

    public async Task DeleteBoardAsync(string boardId)
    {
        try
        {
            var resp = await Client.DeleteAsync($"{Base}/api/boards/{boardId}");
            if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                resp.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Cannot reach Corvida server at {Base}. Check your Server URL setting.", ex);
        }
    }
}
