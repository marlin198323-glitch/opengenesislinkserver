// SPDX-License-Identifier: MPL-2.0
package main

import (
	"strings"
	"testing"
)

func setMediaTestEnv(t *testing.T) {
	t.Helper()
	t.Setenv("OGLVOICE_MEDIA_ACL_JSON", `{"nodes":{"sim-01":{"secret":"abcdefghijklmnopqrstuvwxyz0123456789","tenant":"ogl"}},"tenants":{"ogl":{"max_sessions":0,"max_regions":0}}}`)
	t.Setenv("OGLVOICE_LIVEKIT_URL", "ws://127.0.0.1:7880")
	t.Setenv("OGLVOICE_LIVEKIT_API_KEY", "test-only")
	t.Setenv("OGLVOICE_LIVEKIT_API_SECRET", strings.Repeat("z", 40))
}

func TestDedicatedWebRTCPortRange(t *testing.T) {
	setMediaTestEnv(t)
	t.Setenv("OGLVOICE_WEBRTC_PORT_MIN", "40000")
	t.Setenv("OGLVOICE_WEBRTC_PORT_MAX", "40999")
	cfg, err := loadConfig()
	if err != nil { t.Fatal(err) }
	if cfg.WebRTCPortMin != 40000 || cfg.WebRTCPortMax != 40999 {
		t.Fatalf("unexpected WebRTC ports: %d-%d", cfg.WebRTCPortMin, cfg.WebRTCPortMax)
	}
	pc, err := newViewerPeerConnection(cfg, nil)
	if err != nil { t.Fatal(err) }
	_ = pc.Close()
}

func TestDedicatedWebRTCPortRangeRejectsInvalid(t *testing.T) {
	for _, tc := range []struct{ name, min, max string }{
		{"one-sided", "40000", ""},
		{"reversed", "41000", "40000"},
		{"too-small", "40000", "40002"},
		{"system-port", "80", "1000"},
		{"overflow", "40000", "65536"},
		{"non-numeric", "abc", "41000"},
	} {
		t.Run(tc.name, func(t *testing.T) {
			setMediaTestEnv(t)
			t.Setenv("OGLVOICE_WEBRTC_PORT_MIN", tc.min)
			t.Setenv("OGLVOICE_WEBRTC_PORT_MAX", tc.max)
			if _, err := loadConfig(); err == nil {
				t.Fatal("invalid UDP port range accepted")
			}
		})
	}
}
