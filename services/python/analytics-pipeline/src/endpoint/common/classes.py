import Crypto.Signature.PKCS1_v1_5 as PKCS1_v1_5
import Crypto.Hash.SHA256 as SHA256
import datetime
import requests
import base64
import time


class CloudStorageURLSigner(object):

    """ Contains methods for generating signed URLs for Google Cloud Storage.
    """

    def __init__(self, key, client_id_email, expiration=None, session=None):
        self.key = key
        self.client_id_email = client_id_email
        self.gcs_api_endpoint = 'https://storage.googleapis.com'

        self.expiration = expiration or (datetime.datetime.now() + datetime.timedelta(minutes=30))
        self.expiration = int(time.mktime(self.expiration.timetuple()))

    def base64_sign(self, plaintext):

        """ Signs and returns a base64-encoded SHA256 digest.
        """

        shahash = SHA256.new(plaintext.encode('utf-8'))
        signer = PKCS1_v1_5.new(self.key)
        signature_bytes = signer.sign(shahash)
        return base64.b64encode(signature_bytes)

    def make_signature_string(self, verb, path, content_md5, content_type):

        """ Creates the signature string for signing according to GCS docs.
        """

        signature_string = ('{verb}\n'
                            '{content_md5}\n'
                            '{content_type}\n'
                            '{expiration}\n'
                            '{resource}')
        return signature_string.format(verb=verb, content_md5=content_md5,
          content_type=content_type, expiration=self.expiration, resource=path)

    def make_url(self, verb, path, content_type='', content_md5=''):

        """ Forms and returns the full signed URL to access GCS.
        """

        base_url = '%s%s' % (self.gcs_api_endpoint, path)
        signature_string = self.make_signature_string(verb=verb, path=path, content_md5=content_md5, content_type=content_type)
        signature_signed = self.base64_sign(signature_string)
        query_params = {'GoogleAccessId': self.client_id_email, 'Expires': str(self.expiration), 'Signature': signature_signed}
        return base_url, query_params

    def put(self, path, content_type, md5_digest):
        base_url, query_params = self.make_url(verb='PUT', path=path, content_type=content_type, content_md5=md5_digest)
        headers = {'Content-Type': content_type, 'Content-MD5': md5_digest}
        request = requests.Request('PUT', base_url, params=query_params).prepare()
        return {'signed_url': request.url, 'headers': headers, 'md5_digest': md5_digest, 'statusCode': 200}
