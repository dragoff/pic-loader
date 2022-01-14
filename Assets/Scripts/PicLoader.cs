// based on https://github.com/shamsdev/davinci

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// PicLoader - A Run-Time image downloading and caching library.
/// Ex.
/// PicLoader.Init()
/// 	.Set(artUrl)
/// 	.SetCached(true)
/// 	.Into(renderer)
/// 	.SetFadeTime(0f)
/// 	.SetLoadingPlaceholder(placeholderTexture)
/// 	.Run();
/// </summary>
public class PicLoader : MonoBehaviour
{
	private static readonly string filePath = Application.persistentDataPath + "/" + "PicLoader" + "/";

	private static bool ENABLE_GLOBAL_LOGS = true;

	private bool enableLog = false;
	private float fadeTime = 1;
	private bool cached = true;
	private int timeout = 30;
	private int timeoutAttempts = 3;

	private enum RendererType
	{
		None,
		UiImage,
		Renderer,
		RawImage
	}

	private RendererType rendererType = RendererType.None;
	private GameObject targetObj;
	private string url = null;

	private Texture2D loadingPlaceholder, errorPlaceholder;

	private UnityAction onStartAction,
		onDownloadedAction,
		onLoadedAction,
		onEndAction;

	private UnityAction<int> onDownloadProgressChange;
	private UnityAction<string> onErrorAction;

	private static readonly Dictionary<string, PicLoader> underProcess = new Dictionary<string, PicLoader>();

	private string uniqueHash;
	private int progress;

	/// <summary>
	/// Get instance of picLoader class
	/// </summary>
	public static PicLoader Init()
	{
		return new GameObject("PicLoader").AddComponent<PicLoader>();
	}

	/// <summary>
	/// Set image url for download.
	/// </summary>
	/// <param name="url">Image Url</param>
	/// <returns></returns>
	public PicLoader Set(string url)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Url set : " + url);

		this.url = url;
		return this;
	}

	/// <summary>
	/// Set fading animation time.
	/// </summary>
	/// <param name="fadeTime">Fade animation time. Set 0 for disable fading.</param>
	/// <returns></returns>
	public PicLoader SetFadeTime(float fadeTime)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Fading time set : " + fadeTime);

		this.fadeTime = fadeTime;
		return this;
	}

	/// <summary>
	/// Set target Image component.
	/// </summary>
	/// <param name="image">target Unity UI image component</param>
	/// <returns></returns>
	public PicLoader Into(Image image)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as UIImage set : " + image);

		rendererType = RendererType.UiImage;
		this.targetObj = image.gameObject;
		return this;
	}

	/// <summary>
	/// Set target Renderer component.
	/// </summary>
	/// <param name="renderer">target renderer component</param>
	/// <returns></returns>
	public PicLoader Into(Renderer renderer)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as Renderer set : " + renderer);

		rendererType = RendererType.Renderer;
		this.targetObj = renderer.gameObject;
		return this;
	}

	public PicLoader Into(RawImage rawImage)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as RawImage set : " + rawImage);

		rendererType = RendererType.RawImage;
		this.targetObj = rawImage.gameObject;
		return this;
	}

	#region Actions

	public PicLoader OnStart(UnityAction action)
	{
		this.onStartAction = action;

		if (enableLog)
			Debug.Log("[PicLoader] On start action set : " + action);

		return this;
	}

	public PicLoader OnDownloaded(UnityAction action)
	{
		this.onDownloadedAction = action;

		if (enableLog)
			Debug.Log("[PicLoader] On downloaded action set : " + action);

		return this;
	}

	public PicLoader OnDownloadProgressChanged(UnityAction<int> action)
	{
		this.onDownloadProgressChange = action;

		if (enableLog)
			Debug.Log("[PicLoader] On download progress changed action set : " + action);

		return this;
	}

	public PicLoader OnLoaded(UnityAction action)
	{
		this.onLoadedAction = action;

		if (enableLog)
			Debug.Log("[PicLoader] On loaded action set : " + action);

		return this;
	}

	public PicLoader OnError(UnityAction<string> action)
	{
		this.onErrorAction = action;

		if (enableLog)
			Debug.Log("[PicLoader] On error action set : " + action);

		return this;
	}

	public PicLoader OnEnd(UnityAction action)
	{
		this.onEndAction = action;

		if (enableLog)
			Debug.Log("[PicLoader] On end action set : " + action);

		return this;
	}

	#endregion

	/// <summary>
	/// Show or hide logs in console.
	/// </summary>
	/// <param name="enable">'true' for show logs in console.</param>
	/// <returns></returns>
	public PicLoader SetEnableLog(bool enable)
	{
		this.enableLog = enable;

		if (enable)
			Debug.Log("[PicLoader] Logging enabled : true");

		return this;
	}

	/// <summary>
	/// Set the sprite of image when picLoader is downloading and loading image
	/// </summary>
	/// <param name="placeholder">loading texture</param>
	/// <returns></returns>
	public PicLoader SetLoadingPlaceholder(Texture2D placeholder)
	{
		this.loadingPlaceholder = placeholder;

		if (enableLog)
			Debug.Log("[PicLoader] Loading placeholder has been set.");

		return this;
	}

	/// <summary>
	/// Set image sprite when some error occurred during downloading or loading image
	/// </summary>
	/// <param name="placeholder">error texture</param>
	/// <returns></returns>
	public PicLoader SetErrorPlaceholder(Texture2D placeholder)
	{
		this.errorPlaceholder = placeholder;

		if (enableLog)
			Debug.Log("[PicLoader] Error placeholder has been set.");

		return this;
	}

	/// <summary>
	/// Enable cache
	/// </summary>
	/// <returns></returns>
	public PicLoader SetCached(bool cached)
	{
		this.cached = cached;

		if (enableLog)
			Debug.Log("[PicLoader] Cache enabled : " + cached);

		return this;
	}

	/// <summary>
	/// Set timeout & connection attempts.
	/// </summary>
	/// <param name="timeout">Timeout in sec. Default is 30s.</param>
	/// <param name="attempts">Default is 3.</param>
	/// <returns></returns>
	public PicLoader SetTimeout(int timeout, int attempts)
	{
		this.timeout = timeout;
		this.timeoutAttempts = attempts;

		if (enableLog)
			Debug.Log($"$[PicLoader] Timeout set : {timeout} sec & {timeoutAttempts} attempts");

		return this;
	}

	/// <summary>
	/// Start picLoader process.
	/// </summary>
	public void Run()
	{
		if (url == null)
		{
			Error("Url has not been set. Use 'Load' function to set image url.");
			return;
		}

		try
		{
			var uri = new Uri(url);
			this.url = uri.AbsoluteUri;
		}
		catch (Exception)
		{
			Error("Url is not correct.");
			return;
		}

		if (rendererType == RendererType.None || targetObj == null)
		{
			Error("Target has not been set. Use 'into' function to set target component.");
			return;
		}

		if (enableLog)
			Debug.Log("[PicLoader] Start Working.");

		if (loadingPlaceholder != null)
			SetLoadingImage();

		onStartAction?.Invoke();

		if (!Directory.Exists(filePath))
			Directory.CreateDirectory(filePath);

		uniqueHash = CreateMD5(url);

		if (underProcess.ContainsKey(uniqueHash))
		{
			PicLoader sameProcess = underProcess[uniqueHash];
			sameProcess.onDownloadedAction += () =>
			{
				onDownloadedAction?.Invoke();

				LoadSpriteToImage();
			};
			return;
		}

		if (File.Exists(filePath + uniqueHash))
		{
			onDownloadedAction?.Invoke();
			LoadSpriteToImage();
			return;
		}

		underProcess.Add(uniqueHash, this);
		StopAllCoroutines();
		StartCoroutine(nameof(Downloader));
	}

	private IEnumerator Downloader()
	{
		if (enableLog)
			Debug.Log("[PicLoader] Download started.");

		var attempts = 0;

		UnityWebRequest webRequest;
		do
		{
			webRequest = new UnityWebRequest(url)
			{
				timeout = timeout,
				downloadHandler = new DownloadHandlerBuffer()
			};
			webRequest.SendWebRequest();

			if (attempts++ > 0)
				Debug.Log($"504 Timeout error. Retrying... [attempt: {attempts}]");

			while (!webRequest.isDone)
			{
				if (webRequest.error != null)
				{
					Error("Error while downloading the image : " + webRequest.error);
					yield break;
				}

				progress = Mathf.FloorToInt(webRequest.downloadProgress * 100);
				onDownloadProgressChange?.Invoke(progress);

				if (enableLog)
					Debug.Log("[PicLoader] Downloading progress : " + progress + "%");
				yield return null;
			}
		} while (!webRequest.isDone || webRequest.responseCode == 504 && attempts <= timeoutAttempts);

		if (webRequest.error == null)
			File.WriteAllBytes(filePath + uniqueHash, webRequest.downloadHandler.data);

		webRequest.Dispose();
		webRequest = null;

		onDownloadedAction?.Invoke();

		LoadSpriteToImage();

		underProcess.Remove(uniqueHash);
	}

	private void LoadSpriteToImage()
	{
		progress = 100;
		onDownloadProgressChange?.Invoke(progress);

		if (enableLog)
			Debug.Log("[PicLoader] Downloading progress : " + progress + "%");

		if (!File.Exists(filePath + uniqueHash))
		{
			Error("Loading image file has been failed.");
			return;
		}

		StopAllCoroutines();
		StartCoroutine(ImageLoader());
	}

	private void SetLoadingImage()
	{
		switch (rendererType)
		{
			case RendererType.Renderer:
				Renderer renderer = targetObj.GetComponent<Renderer>();
				renderer.material.mainTexture = loadingPlaceholder;
				break;

			case RendererType.UiImage:
				Image image = targetObj.GetComponent<Image>();
				Sprite sprite = Sprite.Create(loadingPlaceholder,
					new Rect(0, 0, loadingPlaceholder.width, loadingPlaceholder.height),
					new Vector2(0.5f, 0.5f));
				image.sprite = sprite;
				break;
			case RendererType.RawImage:
				RawImage rawImage = targetObj.GetComponent<RawImage>();
				rawImage.texture = loadingPlaceholder;
				break;
		}
	}

	private IEnumerator ImageLoader(Texture2D texture = null)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Start loading image.");

		if (texture == null)
		{
			byte[] fileData;
			fileData = File.ReadAllBytes(filePath + uniqueHash);
			texture = new Texture2D(2, 2);
			//ImageConversion.LoadImage(texture, fileData);
			texture.LoadImage(fileData); //..this will auto-resize the texture dimensions.
		}

		Color color;

		if (targetObj != null)
			switch (rendererType)
			{
				case RendererType.Renderer:
					yield return LoadRenderer();
					break;

				case RendererType.UiImage:
					yield return LoadUiImage();
					break;

				case RendererType.RawImage:
					yield return LoadRawImage();
					break;
			}

		onLoadedAction?.Invoke();

		if (enableLog)
			Debug.Log("[PicLoader] Image has been loaded.");

		Finish();

		// Loaders
		IEnumerator LoadRenderer()
		{
			var renderer = targetObj.GetComponent<Renderer>();

			if (renderer == null || renderer.material == null)
				yield break;

			renderer.material.mainTexture = texture;

			if (fadeTime > 0 && renderer.material.HasProperty("_Color"))
			{
				var material = renderer.material;
				var color = material.color;
				var maxAlpha = color.a;

				color.a = 0;

				material.color = color;
				float time = Time.time;
				while (color.a < maxAlpha)
				{
					color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (renderer != null)
						renderer.material.color = color;

					yield return null;
				}
			}
		}

		IEnumerator LoadUiImage()
		{
			var image = targetObj.GetComponent<Image>();

			if (image == null)
				yield break;

			Sprite sprite = Sprite.Create(texture,
				new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

			image.sprite = sprite;
			var color = image.color;
			var maxAlpha = color.a;

			if (fadeTime > 0)
			{
				color.a = 0;
				image.color = color;

				float time = Time.time;
				while (color.a < maxAlpha)
				{
					color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (image != null)
						image.color = color;
					yield return null;
				}
			}
		}

		IEnumerator LoadRawImage()
		{
			var rawImage = targetObj.GetComponent<RawImage>();

			if (rawImage == null)
				yield break;

			rawImage.texture = texture;
			var color = rawImage.color;
			var maxAlpha = color.a;

			if (fadeTime > 0)
			{
				color.a = 0;
				rawImage.color = color;

				float time = Time.time;
				while (color.a < maxAlpha)
				{
					color.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (rawImage != null)
						rawImage.color = color;
					yield return null;
				}
			}
		}
	}

	private static string CreateMD5(string input)
	{
		// Use input string to calculate MD5 hash
		using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
		byte[] inputBytes = Encoding.ASCII.GetBytes(input);
		byte[] hashBytes = md5.ComputeHash(inputBytes);

		// Convert the byte array to hexadecimal string
		StringBuilder sb = new StringBuilder();
		foreach (var t in hashBytes)
			sb.Append(t.ToString("X2"));

		return sb.ToString();
	}

	private void Error(string message)
	{
		if (enableLog)
			Debug.LogError("[PicLoader] Error : " + message);

		onErrorAction?.Invoke(message);

		if (errorPlaceholder != null)
			StartCoroutine(ImageLoader(errorPlaceholder));
		else Finish();
	}

	private void Finish()
	{
		if (enableLog)
			Debug.Log("[PicLoader] Operation has been finished.");

		if (!cached)
		{
			try
			{
				File.Delete(filePath + uniqueHash);
			}
			catch (Exception ex)
			{
				if (enableLog)
					Debug.LogError($"[PicLoader] Error while removing cached file: {ex.Message}");
			}
		}

		onEndAction?.Invoke();

		Invoke(nameof(Destroyer), 0.5f);
	}

	private void Destroyer()
	{
		Destroy(gameObject);
	}

	/// <summary>
	/// Clear a certain cached file with its url
	/// </summary>
	/// <param name="url">Cached file url.</param>
	/// <returns></returns>
	public static void ClearCache(string url)
	{
		try
		{
			File.Delete(filePath + CreateMD5(url));

			if (ENABLE_GLOBAL_LOGS)
				Debug.Log($"[PicLoader] Cached file has been cleared: {url}");
		}
		catch (Exception ex)
		{
			if (ENABLE_GLOBAL_LOGS)
				Debug.LogError($"[PicLoader] Error while removing cached file: {ex.Message}");
		}
	}

	/// <summary>
	/// Clear all picLoader cached files
	/// </summary>
	/// <returns></returns>
	public static void ClearAllCachedFiles()
	{
		try
		{
			Directory.Delete(filePath, true);

			if (ENABLE_GLOBAL_LOGS)
				Debug.Log("[PicLoader] All PicLoader cached files has been cleared.");
		}
		catch (Exception ex)
		{
			if (ENABLE_GLOBAL_LOGS)
				Debug.LogError($"[PicLoader] Error while removing cached file: {ex.Message}");
		}
	}
}