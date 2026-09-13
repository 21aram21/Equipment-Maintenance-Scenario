using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

/// <summary>
/// Живёт в сцене MenuRoom (это boot-сцена приложения — она в Build Settings).
/// Единственная задача — по нажатию Start загрузить основную сцену процедуры.
/// Level 1 намеренно НЕ лежит в Build Settings — она Addressable и грузится
/// по требованию, это и есть тот самый явный use-case Addressables из ТЗ.
/// </summary>
public class MenuController : MonoBehaviour
{
    [SerializeField] private AssetReference levelSceneReference;

    public void OnStartButtonPressed()
    {
        if (levelSceneReference == null || !levelSceneReference.RuntimeKeyIsValid())
        {
            Debug.LogError("MenuController: levelSceneReference не назначен — не могу загрузить процедуру.");
            return;
        }

        Addressables.LoadSceneAsync(levelSceneReference, LoadSceneMode.Single);
    }
}