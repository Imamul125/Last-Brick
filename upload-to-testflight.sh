#!/bin/bash
echo "--- Starting Fastlane TestFlight Upload ---"

# Pass the credentials securely to fastlane via environment variables
export APP_STORE_CONNECT_API_KEY_KEY_ID="$APP_STORE_CONNECT_KEY_ID"
export APP_STORE_CONNECT_API_KEY_ISSUER_ID="$APP_STORE_CONNECT_ISSUER_ID"
export APP_STORE_CONNECT_API_KEY_KEY="$APP_STORE_CONNECT_API_KEY_CONTENT"

# Unity Cloud Build passes the output directory as the second argument ($2)
OUTPUT_DIR="$2"
echo "Searching for IPA in output directory: $OUTPUT_DIR"

if [ -d "$OUTPUT_DIR" ]; then
    IPA_PATH=$(find "$OUTPUT_DIR" -name "*.ipa" -print -quit)
fi

if [ -z "$IPA_PATH" ]; then
    echo "Could not find IPA in $OUTPUT_DIR, searching everything..."
    # The IPA is often placed a level up or in the artifacts folder
    IPA_PATH=$(find ../ -name "*.ipa" -print -quit)
fi

if [ -z "$IPA_PATH" ]; then
    echo "ERROR: Could not find the .ipa file!"
    exit 1
fi

echo "Found IPA at: $IPA_PATH"

bundle exec fastlane pilot upload --ipa "$IPA_PATH" --skip_submission true --skip_waiting_for_build_processing true
echo "--- Fastlane TestFlight Upload Complete ---"
