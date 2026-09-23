#!/bin/bash
# Unity Cloud Build post-build script: uploads the built IPA to TestFlight.
# Requires env vars APP_STORE_CONNECT_KEY_ID, APP_STORE_CONNECT_ISSUER_ID and
# APP_STORE_CONNECT_API_KEY_CONTENT (the .p8 file contents, raw or base64).
set -e
echo "--- TestFlight upload: post-build script ---"

# UCB passes the build output path as $1; fall back to the working directory.
IPA_PATH=""
for dir in "$1" "$WORKSPACE" "."; do
    if [ -n "$dir" ] && [ -d "$dir" ]; then
        IPA_PATH=$(find "$dir" -name "*.ipa" -print -quit 2>/dev/null)
        [ -n "$IPA_PATH" ] && break
    fi
done

if [ -z "$IPA_PATH" ]; then
    echo "ERROR: No .ipa found (searched: '$1', '$WORKSPACE', '$(pwd)')."
    exit 1
fi
echo "Found IPA at: $IPA_PATH"

for var in APP_STORE_CONNECT_KEY_ID APP_STORE_CONNECT_ISSUER_ID APP_STORE_CONNECT_API_KEY_CONTENT; do
    if [ -z "${!var}" ]; then
        echo "ERROR: Environment variable $var is not set in Unity Cloud."
        exit 1
    fi
done

# Write an App Store Connect API key JSON file for fastlane.
KEY_JSON="$(mktemp -t asc_key).json"
# UCB env vars are single-line, so the .p8 line breaks are usually lost; rebuild
# a well-formed PEM from whatever form (raw, flattened, \n-escaped, base64) was pasted.
ruby -rjson -rbase64 -ropenssl -e '
  raw = ENV["APP_STORE_CONNECT_API_KEY_CONTENT"].gsub("\\n", "\n").strip
  unless raw.include?("PRIVATE KEY")
    decoded = Base64.decode64(raw)
    raw = decoded if decoded.include?("PRIVATE KEY")
  end
  body = raw.gsub(/-----(BEGIN|END) PRIVATE KEY-----/, "").gsub(/[^A-Za-z0-9+\/=]/, "")
  key = "-----BEGIN PRIVATE KEY-----\n#{body.scan(/.{1,64}/).join("\n")}\n-----END PRIVATE KEY-----\n"
  begin
    OpenSSL::PKey.read(key)
  rescue => e
    abort "ERROR: APP_STORE_CONNECT_API_KEY_CONTENT is not a valid .p8 key (#{e.class}). " \
          "Paste the full contents of AuthKey_XXXX.p8 into the Unity Cloud env var."
  end
  File.write(ARGV[0], JSON.generate(
    key_id: ENV["APP_STORE_CONNECT_KEY_ID"],
    issuer_id: ENV["APP_STORE_CONNECT_ISSUER_ID"],
    key: key,
    in_house: false))
' "$KEY_JSON"
trap 'rm -f "$KEY_JSON"' EXIT

fastlane pilot upload \
    --ipa "$IPA_PATH" \
    --api_key_path "$KEY_JSON" \
    --skip_submission true \
    --skip_waiting_for_build_processing true

echo "--- Upload to App Store Connect finished ---"
