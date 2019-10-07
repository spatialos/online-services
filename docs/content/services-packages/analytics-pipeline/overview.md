# The Analytics pipeline

This guide will help you get an analytics pipeline set up for your game, so that you can start instrumenting and collecting data as quickly as possible.

The first two steps of the guide are compulsory, while the last two steps are optional.

## Prerequisites

- This guide assumes you've already run through our [Quickstart]({{urlRoot}}/content/get-started/quickstart). This will ensure you have the correct tools installed and resources provisioned.
- You'll also need a couple of extra service account keys. Navigate to the [service account overview in the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts).
    + Download both a JSON & p12 key from the service account named **Analytics GCS Writer**.
    + Download a JSON key from the service account named **Analytics Endpoint**.
- The `gcloud` cli usually ships with a Python 2 interpreter, whereas we will use Python 3. Run `gcloud topic startup` for [more information](https://cloud.google.com/sdk/install) on how to point `gcloud` to a Python 3.4+ interpreter. You could also use something like [pyenv](https://github.com/pyenv/pyenv) to toggle between Python versions.
