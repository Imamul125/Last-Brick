#!/bin/bash
echo "--- Starting Fastlane TestFlight Upload ---"

# Pass the credentials securely to fastlane via environment variables
export APP_STORE_CONNECT_API_KEY_KEY_ID="$APP_STORE_CONNECT_KEY_ID"
export APP_STORE_CONNECT_API_KEY_ISSUER_ID="$APP_STORE_CONNECT_ISSUER_ID"
export APP_STORE_CONNECT_API_KEY_KEY="$APP_STORE_CONNECT_API_KEY_CONTENT"

echo "Searching for IPA file in the entire workspace..."

# We use "find ." to search the entire current directory and all subdirectories
IPA_PATH=$(find . -name "*.ipa" -print -quit)

# If it is somehow not in the current directory, search one level up
if [ -z "$IPA_PATH" ]; then
    echo "Not found in workspace, searching parent directory..."
    IPA_PATH=$(find ../ -name "*.ipa" -print -quit)
fi

if [ -z "$IPA_PATH" ]; then
    echo "ERROR: Could not find the .ipa file anywhere!"
    exit 1
fi

echo "Found IPA at: $IPA_PATH"

bundle exec fastlane pilot upload --ipa "$IPA_PATH" --skip_submission true --skip_waiting_for_build_processing true
echo "--- Fastlane TestFlight Upload Complete ---"
