﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poor form audio manager, completely isolated and reliant on
/// people implementing it behaving nicely and not touching internals.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public enum SoundCategory
    {
        /// <summary>
        /// This is looping content by default, and is controlled separately.
        /// </summary>
        MUSIC,

        /// <summary>
        /// Not looping by default, controlled separately
        /// </summary>
        SFX
    }

    public static GameAudio Instance { get; private set; }

    // Inspector. Clips are public for external sources to provide them.
    // This is not an advisable strategy because it lets code that interacts
    // with the system tweak internal stuff, but it works.
    [Header("Sources")]
    [SerializeField] private AudioClip _music;
    public AudioClip Button;
    public AudioClip ButtonOrToggleClick;
    public AudioClip SliderTick;
    public AudioClip BubblePop;

    // Interal 
    private List<KeyValuePair<SoundCategory, AudioSource>> _sources;
    private bool _hasInit;
    private int _bubbleQueueCount;
    Coroutine _bubblesPopping;

    /// <summary>
    /// Configure data structure
    /// </summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("Preventing duplicate GameAudio from being created");
            DestroyImmediate(this.gameObject);
            return;
        }

        _bubblesPopping = null;
        _hasInit = false;
        _bubbleQueueCount = 0;
        Instance = this;
        DontDestroyOnLoad(this);
    }

    public void PopBubble()
    {
        ++_bubbleQueueCount;
    }

    public void PlaySoundEffect(AudioClip clip)
    {
        Play(clip, SoundCategory.SFX);
    }

    private void Start()
    {
        _sources = new List<KeyValuePair<SoundCategory, AudioSource>>();

        GetCore().events.OnGameInitialized += OnInitialized;
    }

    private void OnInitialized()
    {
        if(_hasInit)
        {
            return;
        }

        ConnectEvents();

        // Play music to start
        Play(_music, SoundCategory.MUSIC);
        _hasInit = true;
    }

    private GameCore GetCore()
    {
        GameCore core = FindObjectOfType<GameCore>();
        return core;
    }

    private DataGeneral GetGameData()
    {
        GameCore core = GetCore();
        DataGeneral data = core.data.GetDataGeneral();

        ConnectEvents();

        return data;
    }

    /// <summary>
    /// We can assume the last events were completely discarded off the core when we are here.
    /// This means we never need to disconnect or anything since the last set of things that
    /// could even dispatch these doesn't exist.
    /// 
    /// This is dangerous, god forbid someone were to use a separate static anything...
    /// </summary>
    private void ConnectEvents()
    {
        Events events = GetCore().events;
        events.OnGameInitialized += OnInitialized;
        events.OnUpdateMusicVolume += OnMusicVolChange;
        events.OnUpdateSFXVolume += OnSFXVolChange;
    }

    private void OnMusicVolChange(float newVol)
    {
        SetCategoryVol(SoundCategory.MUSIC, newVol);
    }

    private void OnSFXVolChange(float newVol)
    {
        SetCategoryVol(SoundCategory.SFX, newVol);
    }

    private void SetCategoryVol(SoundCategory category, float newVol)
    {
        foreach (KeyValuePair<SoundCategory, AudioSource> pair in _sources)
        {
            if (pair.Key == category)
            {
                pair.Value.volume = newVol;
            }
        }
    }

    /// <summary>
    /// Creates and plays a new sound, adding it to managed list.
    /// </summary>
    private void Play(AudioClip toPlay, SoundCategory category)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = toPlay;
        source.Play();

        if(category == SoundCategory.MUSIC)
        {
            source.loop = true;
            float vol = GetGameData().musicVolume;
            source.volume = vol;
        }
        else if (category == SoundCategory.SFX)
        {
            source.loop = false;
            float vol = GetGameData().sfxVolume;
            source.volume = vol;
        }

        _sources.Add(new KeyValuePair<SoundCategory, AudioSource>(category, source));
    }

    /// <summary>
    /// Upkeep the audio source list so we don't keep sounds done playing
    /// </summary>
    private void Update()
    {
        if(_bubbleQueueCount > 0 && _bubblesPopping == null)
        {
            _bubblesPopping = StartCoroutine(RandomlyCreateBubbleNoise());
        }

        for (int i = _sources.Count - 1; i >= 0; i--)
        {
            AudioSource source = _sources[i].Value;
            if (!source.loop && !source.isPlaying)
            {
                Destroy(source);
                _sources.RemoveAt(i);
            }
        }
    }


    private IEnumerator RandomlyCreateBubbleNoise()
    {
        // Tick down, but prevent duplicate calls along the way.
        if (_bubbleQueueCount <= 0)
        {
            _bubblesPopping = null;
            yield break;
        }
        
        --_bubbleQueueCount;
        float toWait = UnityEngine.Random.Range(0.0033f, 0.05f);
        yield return null;
        yield return new WaitForSeconds(toWait);
        Play(BubblePop, SoundCategory.SFX);

        _bubblesPopping = StartCoroutine(RandomlyCreateBubbleNoise());
    }

}
