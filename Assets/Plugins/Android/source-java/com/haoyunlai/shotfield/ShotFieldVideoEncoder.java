package com.haoyunlai.shotfield;

import android.content.Context;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.os.Environment;

import java.io.File;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/**
 * ShotField in-app MP4 encoder bridge (route 1-B: buffer readback, no NDK).
 *
 * First version: AVC video track only, no audio track. Called from Unity C#
 * through AndroidJavaObject; every method is expected on the Unity thread.
 *
 * Input frames are RGBA8888 bytes; the encoder converts them to NV12
 * (COLOR_FormatYUV420SemiPlanar) before queueing them to MediaCodec.
 */
public class ShotFieldVideoEncoder {

    private static final long TIMEOUT_US = 10000L;

    private final Context context;
    private MediaCodec codec;
    private MediaMuxer muxer;
    private int trackIndex = -1;
    private boolean muxerStarted;
    private boolean configured;
    private boolean stopped;

    private int width;
    private int height;
    private int frameCount;
    private long firstFrameUs = -1L;
    private long lastFrameUs;
    private String outputPath;
    private String error;

    public ShotFieldVideoEncoder(Context context) {
        this.context = context;
    }

    /**
     * Configure codec and muxer, create the app-specific Movies directory.
     *
     * @param width   frame width in pixels
     * @param height  frame height in pixels
     * @param fps     target frame rate
     * @param bitrate AVC bit rate in bits per second
     * @param fileName output mp4 file name (directory resolved on device)
     */
    public synchronized void configure(int width, int height, int fps, int bitrate, String fileName)
            throws IOException {
        if (configured) throw new IllegalStateException("encoder already configured");
        this.width = width;
        this.height = height;

        File dir = context.getExternalFilesDir(Environment.DIRECTORY_MOVIES);
        if (dir == null) dir = context.getExternalFilesDir(null);
        if (dir == null) throw new IOException("external files dir unavailable");
        if (!dir.exists() && !dir.mkdirs()) {
            throw new IOException("cannot create dir: " + dir.getAbsolutePath());
        }
        outputPath = new File(dir, fileName).getAbsolutePath();

        MediaFormat format = MediaFormat.createVideoFormat(
                MediaFormat.MIMETYPE_VIDEO_AVC, width, height);
        format.setInteger(MediaFormat.KEY_COLOR_FORMAT,
                MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420SemiPlanar);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
        format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
        format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);

        codec = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
        codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
        codec.start();

        muxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
        configured = true;
    }

    /**
     * Feed one RGBA8888 frame. Timestamps are in microseconds and do not need
     * to start at zero; the first frame is remapped to zero by the encoder.
     */
    public synchronized void pushFrame(byte[] rgba, long timestampUs) {
        if (!configured || stopped) throw new IllegalStateException("encoder not ready");
        if (rgba == null || rgba.length != width * height * 4) {
            throw new IllegalArgumentException("rgba size mismatch: expected "
                    + (width * height * 4) + " got " + (rgba == null ? 0 : rgba.length));
        }

        if (firstFrameUs < 0) firstFrameUs = timestampUs;
        lastFrameUs = timestampUs;

        int index = codec.dequeueInputBuffer(TIMEOUT_US);
        if (index < 0) throw new IllegalStateException("encoder input buffer busy");
        ByteBuffer input = codec.getInputBuffer(index);
        input.clear();
        input.order(ByteOrder.LITTLE_ENDIAN);
        byte[] nv12 = convertRgbaToNv12(rgba, width, height);
        input.put(nv12);

        int flags = frameCount == 0 ? MediaCodec.BUFFER_FLAG_KEY_FRAME : 0;
        codec.queueInputBuffer(index, 0, nv12.length,
                timestampUs - firstFrameUs, flags);
        frameCount++;
        drain(false);
    }

    /**
     * Signal end of stream, flush remaining encoded samples, finalize the mp4.
     * Throws IllegalStateException when the output is empty (0 frames or
     * 0 bytes) so the caller never ships a silent broken file.
     */
    public synchronized void stop() {
        if (!configured || stopped) return;
        stopped = true;
        try {
            int eosIndex = codec.dequeueInputBuffer(TIMEOUT_US);
            if (eosIndex >= 0) {
                codec.queueInputBuffer(eosIndex, 0, 0,
                        lastFrameUs - firstFrameUs, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
            }
            drain(true);
            if (muxer != null && muxerStarted) muxer.stop();

            File out = new File(outputPath);
            long bytes = out.exists() ? out.length() : -1L;
            if (frameCount == 0 || bytes <= 0) {
                error = "empty output: frames=" + frameCount + " bytes=" + bytes;
                throw new IllegalStateException(error);
            }
        } finally {
            release();
        }
    }

    public synchronized void release() {
        stopped = true;
        if (codec != null) {
            try { codec.stop(); } catch (Exception ignored) { }
            try { codec.release(); } catch (Exception ignored) { }
            codec = null;
        }
        if (muxer != null) {
            try { if (muxerStarted) muxer.stop(); } catch (Exception ignored) { }
            try { muxer.release(); } catch (Exception ignored) { }
            muxer = null;
        }
        muxerStarted = false;
        trackIndex = -1;
    }

    public String getOutputPath() { return outputPath; }
    public long getDurationMs() {
        if (firstFrameUs < 0) return 0L;
        return Math.max(0L, (lastFrameUs - firstFrameUs) / 1000L);
    }
    public long getFileSizeBytes() {
        File f = new File(outputPath);
        return f.exists() ? f.length() : 0L;
    }
    public int getWidth() { return width; }
    public int getHeight() { return height; }
    public int getFrameCount() { return frameCount; }
    public String getError() { return error; }

    private void drain(boolean untilEos) {
        MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        while (true) {
            int index = codec.dequeueOutputBuffer(info, untilEos ? TIMEOUT_US : 0L);
            if (index == MediaCodec.INFO_TRY_AGAIN_LATER) break;
            if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (!muxerStarted) {
                    trackIndex = muxer.addTrack(codec.getOutputFormat());
                    muxer.start();
                    muxerStarted = true;
                }
                continue;
            }
            if (index < 0) continue;

            ByteBuffer out = codec.getOutputBuffer(index);
            if (muxerStarted && out != null && info.size > 0) {
                out.position(info.offset);
                out.limit(info.offset + info.size);
                muxer.writeSampleData(trackIndex, out, info);
            }
            codec.releaseOutputBuffer(index, false);

            if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) break;
            if (!untilEos) break;
        }
    }

    private static byte[] convertRgbaToNv12(byte[] rgba, int w, int h) {
        int ySize = w * h;
        byte[] nv12 = new byte[ySize + ySize / 2];
        for (int j = 0; j < h; j++) {
            for (int i = 0; i < w; i++) {
                int p = (j * w + i) * 4;
                int r = rgba[p] & 0xff;
                int g = rgba[p + 1] & 0xff;
                int b = rgba[p + 2] & 0xff;
                int y = (int) (16 + (65.738 * r + 129.057 * g + 25.064 * b) / 256.0);
                nv12[j * w + i] = (byte) clamp(y);
                if ((i & 1) == 0 && (j & 1) == 0) {
                    int u = (int) (128 + (-37.949 * r - 74.203 * g + 112.772 * b) / 256.0);
                    int v = (int) (128 + (112.772 * r - 93.786 * g - 18.214 * b) / 256.0);
                    int q = ySize + ((j >> 1) * (w >> 1) + (i >> 1)) * 2;
                    nv12[q] = (byte) clamp(u);
                    nv12[q + 1] = (byte) clamp(v);
                }
            }
        }
        return nv12;
    }

    private static int clamp(int v) {
        return v < 0 ? 0 : (v > 255 ? 255 : v);
    }
}
