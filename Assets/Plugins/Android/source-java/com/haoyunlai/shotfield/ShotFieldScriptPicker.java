package com.haoyunlai.shotfield;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

/**
 * SAF (Storage Access Framework) picker bridge for script documents.
 *
 * Supported MIME types: plain text, markdown, docx, pdf. The picker is
 * started from C# through AndroidJavaObject. The host activity must forward
 * its onActivityResult to handleActivityResult(); that glue lives outside
 * this plugin until the launcher activity subclass is wired up.
 *
 * Result delivery back to Unity uses UnityPlayer.UnitySendMessage:
 *   GameObject : ShotFieldScriptImport
 *   Success    : OnScriptUriPicked, payload "uri|displayName"
 *   Failure    : OnScriptPickFailed, payload reason
 */
public class ShotFieldScriptPicker {

    public static final int REQUEST_SCRIPT_FILE = 4721;
    public static final String GAME_OBJECT = "ShotFieldScriptImport";
    public static final String METHOD_URI_PICKED = "OnScriptUriPicked";
    public static final String METHOD_PICK_FAILED = "OnScriptPickFailed";

    private static final String LOG_TAG = "ShotFieldPicker";

    private final Activity activity;

    public ShotFieldScriptPicker(Activity activity) {
        this.activity = activity;
    }

    /**
     * Launch the SAF document picker. Returns false when no picker activity
     * exists on the device; failure is also pushed to Unity via OnScriptPickFailed.
     */
    public boolean pickScriptFile() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("*/*");
        intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {
                "text/plain",
                "text/markdown",
                "text/x-markdown",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/pdf"
        });
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        try {
            activity.startActivityForResult(intent, REQUEST_SCRIPT_FILE);
            return true;
        } catch (ActivityNotFoundException e) {
            notifyFailure("No SAF document picker available: " + e.getMessage());
            return false;
        }
    }

    /**
     * Forward the activity result. Called from the host activity's
     * onActivityResult glue. Grants persistable read permission before
     * sending the URI back to Unity.
     */
    public void handleActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST_SCRIPT_FILE) return;
        if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
            notifyFailure("Picker cancelled or returned no document URI.");
            return;
        }

        Uri uri = data.getData();
        try {
            activity.getContentResolver().takePersistableUriPermission(uri,
                    Intent.FLAG_GRANT_READ_URI_PERMISSION);
        } catch (SecurityException e) {
            Log.w(LOG_TAG, "Persistable URI permission rejected: " + e.getMessage());
        }

        String displayName = queryDisplayName(uri);
        UnityPlayer.UnitySendMessage(GAME_OBJECT, METHOD_URI_PICKED,
                uri.toString() + "|" + displayName);
    }

    private String queryDisplayName(Uri uri) {
        Cursor cursor = null;
        try {
            cursor = activity.getContentResolver().query(uri, null, null, null, null);
            if (cursor != null && cursor.moveToFirst()) {
                int idx = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (idx >= 0) return cursor.getString(idx);
            }
        } catch (Exception e) {
            Log.w(LOG_TAG, "Display name query failed: " + e.getMessage());
        } finally {
            if (cursor != null) cursor.close();
        }
        return "";
    }

    private void notifyFailure(String reason) {
        UnityPlayer.UnitySendMessage(GAME_OBJECT, METHOD_PICK_FAILED, reason);
    }
}
