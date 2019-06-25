import Crypto.Signature.PKCS1_v1_5 as PKCS1_v1_5
import Crypto.Hash.SHA256 as SHA256

import datetime
import requests
import base64
import time

class CloudStorageURLSigner(object):
    """Contains methods for generating signed URLs for Google Cloud Storage."""

    def __init__(self, key, client_id_email, expiration = None, session = None):
        self.key = key
        self.client_id_email = client_id_email
        self.gcs_api_endpoint = 'https://storage.googleapis.com'

        self.expiration = expiration or (datetime.datetime.now() + datetime.timedelta(days=1/48))
        self.expiration = int(time.mktime(self.expiration.timetuple()))

    def _Base64Sign(self, plaintext):
        """Signs and returns a base64-encoded SHA256 digest."""
        shahash = SHA256.new(plaintext.encode('utf-8'))
        signer = PKCS1_v1_5.new(self.key)
        signature_bytes = signer.sign(shahash)
        return base64.b64encode(signature_bytes)

    def _MakeSignatureString(self, verb, path, content_md5, content_type):
        """Creates the signature string for signing according to GCS docs."""
        signature_string = ('{verb}\n'
                            '{content_md5}\n'
                            '{content_type}\n'
                            '{expiration}\n'
                            '{resource}')
        return signature_string.format(verb = verb, content_md5 = content_md5, content_type = content_type, expiration = self.expiration, resource = path)

    def _MakeUrl(self, verb, path, content_type='', content_md5=''):
        """Forms and returns the full signed URL to access GCS."""
        base_url = '%s%s' % (self.gcs_api_endpoint, path)
        signature_string = self._MakeSignatureString(verb = verb, path = path, content_md5 = content_md5, content_type = content_type)
        signature_signed = self._Base64Sign(signature_string)
        query_params = {'GoogleAccessId': self.client_id_email, 'Expires': str(self.expiration), 'Signature': signature_signed}
        return base_url, query_params

    def Put(self, path, content_type, md5_digest):
        base_url, query_params = self._MakeUrl(verb = 'PUT', path = path, content_type = content_type, content_md5 = md5_digest)
        headers = {}
        headers['Content-Type'], headers['Content-MD5'] = content_type, md5_digest
        request = requests.Request('PUT', base_url, params = query_params).prepare()
        return {'signed_url': request.url, 'headers': headers, 'md5_digest': md5_digest, 'code': 200}
