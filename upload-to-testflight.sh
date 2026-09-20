#!/bin/bash
echo "--- Starting Fastlane TestFlight Upload ---"
if ! command -v fastlane &> /dev/null
then
    echo "fastlane could not be found, attempting to install..."
    gem install fastlane -NV
fi

IPA_PATH=$(find . -name "*.ipa" -print -quit)
if [ -z "$IPA_PATH" ]; then
    echo "ERROR: Could not find the .ipa file!"
    exit 1
fi
echo "Found IPA at: $IPA_PATH"

fastlane pilot upload --ipa "$IPA_PATH" --api_key "{\`"key_id\`":\`"$APP_STORE_CONNECT_KEY_ID\`", \`"issuer_id\`":\`"$APP_STORE_CONNECT_ISSUER_ID\`", \`"key\`":\`"$APP_STORE_CONNECT_API_KEY_CONTENT\`"}" --skip_submission true --skip_waiting_for_build_processing true
echo "--- Fastlane TestFlight Upload Complete ---"
