﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Xamarin.CommunityToolkit.UI.Views
{
	public partial class GravatarImageSourceHandler
	{
		const string requestUriFormat = "https://www.gravatar.com/avatar/{0}?s={1}&d={2}";
		static readonly Lazy<HttpClient> lazyHttp = new(() => new HttpClient());
		static readonly SemaphoreSlim semaphore = new(1);

		public static async Task<FileInfo?> LoadInternal(ImageSource imageSource, float scale, string cacheDirectory)
		{
			if (imageSource is GravatarImageSource gis)
			{
				var cacheFilePath = Path.Combine(cacheDirectory, nameof(GravatarImageSource), CacheFileName(gis, scale));
				var cacheFileInfo = new FileInfo(cacheFilePath);

				if (!await UseCacheFile(gis.CachingEnabled, gis.CacheValidity, cacheFileInfo))
				{
					_ = gis.Email ?? throw new InvalidOperationException($"{nameof(gis.Email)} is not initialized");
					var imageBytes = await GetGravatarAsync(gis.Email, gis.Size, scale, gis.Default);
					await SaveImage(cacheFileInfo, imageBytes);
				}

				return cacheFileInfo;
			}

			return null;
		}

		static async Task SaveImage(FileInfo cacheFileInfo, byte[] imageBytes)
		{
			if (imageBytes.Length < 1)
				return;

			try
			{
				await semaphore.WaitAsync();

				// Delete Cached File
				if (cacheFileInfo.Exists)
					cacheFileInfo.Delete();

#if NET472
				File.WriteAllBytes(cacheFileInfo.FullName, imageBytes);
#else
				await File.WriteAllBytesAsync(cacheFileInfo.FullName, imageBytes);
#endif
				cacheFileInfo.Refresh();
			}
			finally
			{
				semaphore.Release();
			}
		}

		static async Task<bool> UseCacheFile(bool cachingEnabled, TimeSpan cacheValidity, FileInfo file)
		{
			try
			{
				await semaphore.WaitAsync();

				if (file.Directory is { Exists: false })
					file.Directory.Create();
			}
			finally
			{
				semaphore.Release();
			}

			return cachingEnabled && file.Exists && file.CreationTime.Add(cacheValidity) > DateTime.Now;
		}

		static string CacheFileName(GravatarImageSource gis, float scale)
		{
			_ = gis.Email ?? throw new InvalidOperationException($"{nameof(gis.Email)} cannot be null");
			return $"{GetMd5Hash(gis.Email)}-{gis.Size}@{scale}x.png";
		}

		static async Task<byte[]> GetGravatarAsync(string email, int size, float scale, DefaultGravatar defaultGravatar)
		{
			var requestUri = GetGravatarUri(email, size, scale, defaultGravatar);
			try
			{
				using var response = await lazyHttp.Value.GetAsync(requestUri);

				if (!response.IsSuccessStatusCode)
					return Array.Empty<byte>();

				return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
			}
			catch (Exception e)
			{
				Trace.WriteLine(e.Message);
				return Array.Empty<byte>();
			}
		}

		static string GetGravatarUri(string email, int size, float scale, DefaultGravatar defaultGravatar)
			=> string.Format(requestUriFormat, GetMd5Hash(email), size * scale, DefaultGravatarName(defaultGravatar));

		static string DefaultGravatarName(DefaultGravatar defaultGravatar)
			=> defaultGravatar switch
			{
				DefaultGravatar.FileNotFound => "404",
				DefaultGravatar.MysteryPerson => "mp",
				_ => $"{defaultGravatar}".ToLower(),
			};

		static string GetMd5Hash(string str)
		{
			using var md5 = MD5.Create();
			var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(str));

			var sBuilder = new StringBuilder();

			foreach (var hashByte in hash)
			{
				sBuilder.Append(hashByte.ToString("x2"));
			}

			return sBuilder.ToString();
		}
	}
}