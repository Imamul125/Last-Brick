#!/bin/bash
echo "--- Post-Build Script Triggered ---"
echo "If this runs before Xcode, the IPA will not be found."
IPA_PATH=$(find . -name "*.ipa" -print -quit)

if [ -z "$IPA_PATH" ]; then
    echo "IPA not found. Exiting gracefully with 0 so Unity Cloud Build can proceed to Xcode compilation."
    exit 0
fi

echo "Found IPA at: $IPA_PATH"
export APP_STORE_CONNECT_API_KEY_KEY_ID="$APP_STORE_CONNECT_KEY_ID"
export APP_STORE_CONNECT_API_KEY_ISSUER_ID="$APP_STORE_CONNECT_ISSUER_ID"
export APP_STORE_CONNECT_API_KEY_KEY="$APP_STORE_CONNECT_API_KEY_CONTENT"

bundle exec fastlane pilot upload --ipa "$IPA_PATH" --skip_submission true --skip_waiting_for_build_processing true
exit 0
