package com.haoyunlai.shotfield;

import android.content.Intent;
import com.unity3d.player.UnityPlayerActivity;

/**
 * Launcher activity subclass for SAF result routing.
 *
 * Only onActivityResult is overridden: super first keeps Unity's own
 * callback chain intact, then the result is forwarded to the script picker.
 * Unknown request codes are ignored by the picker. onCreate and Unity
 * initialization are untouched; no extra permission is declared here.
 */
public class ShotFieldUnityActivity extends UnityPlayerActivity {

    private ShotFieldScriptPicker scriptPicker;

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (scriptPicker == null) {
            scriptPicker = new ShotFieldScriptPicker(this);
        }
        scriptPicker.handleActivityResult(requestCode, resultCode, data);
    }
}
