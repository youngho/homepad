#if UNITY_EDITOR
using Homepad.Core;
using UnityEditor;
using UnityEngine;

namespace Homepad.Editor
{
    public static class KocomHexValidator
    {
        [MenuItem("Tools/Kocom/Validate All Hex Presets")]
        public static void ValidatePresets()
        {
            string mdPath = KocomMarkdownParser.FindMarkdownPath();
            Debug.Log($"<color=#55AAFF>[KocomHexValidator] 마크다운 파일 탐색: {(string.IsNullOrEmpty(mdPath) ? "없음" : mdPath)}</color>");

            KocomHexPresets.Reload();
            var presets = KocomHexPresets.AllPresets;
            int passCount = 0;
            int failCount = 0;

            Debug.Log($"<color=#55AAFF>[KocomHexValidator] 총 {presets.Count}개 프리셋 검증 시작...</color>");

            foreach (var p in presets)
            {
                byte[] bytes = p.rawBytes;
                if (bytes == null || bytes.Length != KocomProtocol.PacketSize)
                {
                    Debug.LogError($"[검증 실패] <b>[{p.category}] {p.title}</b>: 바이트 길이 불일치 (현재: {bytes?.Length ?? 0}바이트, 필요: 21바이트)");
                    failCount++;
                    continue;
                }

                if (bytes[0] != KocomProtocol.Header1 || bytes[1] != KocomProtocol.Header2)
                {
                    Debug.LogError($"[검증 실패] <b>[{p.category}] {p.title}</b>: 헤더 오류 (0x{bytes[0]:X2} 0x{bytes[1]:X2}, 기대: AA 55)");
                    failCount++;
                    continue;
                }

                if (bytes[19] != KocomProtocol.Trailer || bytes[20] != KocomProtocol.Trailer)
                {
                    Debug.LogError($"[검증 실패] <b>[{p.category}] {p.title}</b>: 트레일러 오류 (0x{bytes[19]:X2} 0x{bytes[20]:X2}, 기대: 0D 0D)");
                    failCount++;
                    continue;
                }

                byte expectedChecksum = KocomProtocol.ComputeChecksum(bytes);
                if (bytes[18] != expectedChecksum)
                {
                    Debug.LogError($"[체크섬 오류] <b>[{p.category}] {p.title}</b>: 기록된 체크섬(0x{bytes[18]:X2}) != 올바른 체크섬(0x{expectedChecksum:X2})\n추천 수정: {KocomHexPresets.RecalculateChecksum(p.hexString)}");
                    failCount++;
                    continue;
                }

                if (KocomProtocol.TryParse(bytes, out var frame))
                {
                    string decoded = KocomProtocol.DecodeFrame(frame);
                    Debug.Log($"[검증 통과] <b>[{p.category}] {p.title}</b> -> {decoded} (HEX: {p.hexString})");
                    passCount++;
                }
                else
                {
                    Debug.LogWarning($"[파싱 주의] <b>[{p.category}] {p.title}</b>: Frame 파싱 실패 (체크섬은 정상)");
                    passCount++;
                }
            }

            if (failCount == 0)
            {
                Debug.Log($"<color=#55FF55><b>[검증 완료 - ALL PASS]</b> 모든 프리셋({passCount}개)이 정상입니다!</color>");
            }
            else
            {
                Debug.LogError($"<color=#FF5555><b>[검증 완료 - 오류 발견]</b> 성공: {passCount}개 / 오류: {failCount}개</color>");
            }
        }
    }
}
#endif
