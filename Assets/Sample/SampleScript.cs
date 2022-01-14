using UnityEngine;
using UnityEngine.UI;

public class SampleScript : MonoBehaviour
{
	// Start is called before the first frame update
	void Start()
	{
		PicLoader.Init()
			.Set("https://picsum.photos/200")
			.SetCached(true)
			.Into(GetComponent<Image>())
			.Run();
	}
}