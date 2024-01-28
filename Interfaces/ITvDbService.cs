﻿using FileRenamer.Api.Models;

namespace FileRenamer.Api.Interfaces
{
    public interface ITvDbService
    {
        Task<string> GetTvDbToken();
        Task<TvDbResponse> SearchShowsOrMoviesAsync(string query);
        Task<Root> GetEpisodeDetailsAsync(int id, string season, string episode);
        Task<MovieDetailModel> GetMovieDetailsAsync(int id);
    }
}
