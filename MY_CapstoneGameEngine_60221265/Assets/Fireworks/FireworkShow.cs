using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Fireworks
{
    // Keep the original numeric values so existing serialized cues still play correctly.
    public enum BloomShape { Peony, Ring, Willow, Heart, Saturn, Pinwheel, StarCluster, Image }

    [Serializable]
    public struct FireworkCue
    {
        [Min(0)] public float launchTime;
        public Vector2 launchViewport;
        public Vector2 burstViewport;
        [Min(0.1f)] public float flightTime;
        [Min(0.1f)] public float bloomScale;
        public BloomShape shape;
        public Color petals;
        public Color tips;
    }

    public sealed class FireworkShow : MonoBehaviour
    {
        [Header("Image Firework")]
        [Tooltip("사용할 이미지 또는 마스크. Read/Write 옵션을 켤 필요가 없습니다.")]
        [SerializeField] private Texture2D sourceImage;
        [Tooltip("Auto는 투명 컬러 이미지의 원래 색을 사용하고, 불투명 흑백 마스크의 밝기를 불씨의 강도로 사용합니다.")]
        [SerializeField] private ImageSampling sampling = ImageSampling.Auto;
        [Header("Audio")]
        [Tooltip("공연 중 재생할 음원 파일을 여기에 드래그하세요.")]
        [SerializeField] private AudioClip backgroundMusic;

        [Header("Show")]
        [SerializeField] private Camera showCamera;
        [SerializeField] private SpriteRenderer backdrop;
        [SerializeField] private FireworkShell shellPrefab;
        [SerializeField] private Button playButton;
        [SerializeField] private Sprite startArtwork;
        [SerializeField] private Sprite replayArtwork;
        [SerializeField] private FireworkCue[] cues;

        private readonly List<FireworkShell> liveShells = new List<FireworkShell>();
        private AudioSource musicSource;
        private bool playing;

        private void OnEnable()
        {
            FitBackdrop();
            playButton.image.sprite = startArtwork;
            playButton.gameObject.SetActive(true);
            playButton.onClick.AddListener(PlayShow);
        }

        private void LateUpdate() => FitBackdrop();

        private void FitBackdrop()
        {
            float viewWidth = 2f * showCamera.orthographicSize * showCamera.aspect;
            float stretch = Mathf.Max(1f, viewWidth / backdrop.sprite.bounds.size.x);
            backdrop.transform.localScale = new Vector3(stretch, 1f, 1f);
        }

        private void PlayShow()
        {
            if (playing) return;
            playing = true;
            playButton.gameObject.SetActive(false);
            StartCoroutine(Perform());
        }

        private void StartMusic()
        {
            if (!backgroundMusic) return;
            if (!musicSource)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
                musicSource.loop = true;
            }
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }

        private IEnumerator Perform()
        {
            ImageFireworkPattern pattern = ImageFireworkPattern.Sample(sourceImage, sampling);
            StartMusic();
            float startTime = Time.time;
            bool skippedIntro = false;
            for (int i = 0; i < cues.Length; i++)
            {
                FireworkCue cue = cues[i];
                if (cue.shape == BloomShape.Image && pattern == null)
                {
                    if (i == 0) skippedIntro = true;
                    continue;
                }
                if (skippedIntro)
                {
                    startTime = Time.time - cue.launchTime;
                    skippedIntro = false;
                }
                while (Time.time - startTime < cue.launchTime) yield return null;
                var shell = Instantiate(shellPrefab, transform);
                liveShells.Add(shell);
                float scale = cue.bloomScale * Mathf.Min(1f, showCamera.aspect / 1.4f);
                shell.Launch(cue, StagePoint(cue.launchViewport), StagePoint(cue.burstViewport),
                    scale, 5641 + i * 131, ShellFinished, pattern);
            }

            while (liveShells.Count > 0) yield return null;
            if (musicSource) musicSource.Stop();
            playing = false;
            playButton.image.sprite = replayArtwork;
            playButton.gameObject.SetActive(true);
            playButton.Select();
        }

        private Vector3 StagePoint(Vector2 viewport)
        {
            return showCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10f));
        }

        private void ShellFinished(FireworkShell shell) => liveShells.Remove(shell);

        private void OnDisable()
        {
            playButton.onClick.RemoveListener(PlayShow);
            StopAllCoroutines();
            if (musicSource) musicSource.Stop();
            foreach (FireworkShell shell in liveShells)
                if (shell) shell.Cancel();
            liveShells.Clear();
            playing = false;
        }
    }
}
