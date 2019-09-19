import datetime
import hashlib
import json
import time
import gzip
import io

def parse_none_or_string(value):

    """ This function is primarily used to parse command-line arguments. Whenever
    the string 'None' is passed, it will return it as None, otherwise it will return
    whatever it was passed as a string.
    """

    if value == 'None':
        return None
    return value


def validate_date(date):

    """ This function validates whether the date passed to it is valid. If so, it
    returns True, otherwise False.
    """

    try:
        datetime.datetime.strptime(date, '%Y-%m-%d')
        return True
    except ValueError:
        return False


def generate_date_range(ds_start, ds_stop):

    """ This function returns a date range generator. The arguments determine the
    start & stop dates respectively.
    """

    if not ds_start or not ds_stop:
        yield ''
    else:
        if validate_date(ds_start) and validate_date(ds_stop):
            start = datetime.datetime.strptime(ds_start, '%Y-%m-%d')
            end = datetime.datetime.strptime(ds_stop, '%Y-%m-%d')
            step = datetime.timedelta(days=1)
            while start <= end:
                yield str(start.date())
                start += step
        else:
            raise ValueError('No valid date(s) passed to generate_date_range()!')


def generate_gcs_file_list(bucket_name, environment_list, category_list, ds_start, ds_stop, time_part_list, scale_test_name=''):

    """ This function generates a list of gspath prefixes, which we can subsequently use to retrieve all files matching them.
    Note that None values are parsed as empty strings ('').
    """

    for environment in environment_list:
        for category in category_list:
            for ds in generate_date_range(ds_start, ds_stop):
                for time_part in time_part_list:
                    yield 'gs://{bucket_name}/data_type=json/analytics_environment={environment}/event_category={category}/event_ds={ds}/event_time={time_part}/{scale_test_name}'.format(
                      bucket_name=bucket_name, environment=environment or '', category=category or '', ds=ds or '', time_part=time_part or '', scale_test_name=scale_test_name or '')


def parse_gspath(path, key):

    """ This function is used to extract information from GCS URIs, which should
    contain the following ../key=value/.. format:

    gs://[your Google project id]-analytics/data_type=json/analytics_environment=function/...

    When for instance passing the above path & 'data_type=' as the key it will return its value 'json'.
    """

    try:
        # Try to split the path by key (indexing to 1 will fail if key not present in path):
        value = path.split(key)[1].split('/')[0]
        if key == 'event_ds=' and not validate_date(value):
            return None
        return value
    except Exception:
        return None


def parse_argument(argument, all_list, all_type, all_key='all'):

    """ This function can be used to parse certain command-line arguments.

    If the argument matches the all_key, it returns all_list & a specific name for
    all_list, of which the latter is to be used in the job_name of the Dataflow pipeline.

    Otherwise, it will just return the argument value nested in a list, alongside the
    argument value as the name to be used in the job_name of the Dataflow pipeline.
    """

    if argument == all_key:
        return all_list, '{all_key}-{all_type}'.format(all_key=all_key, all_type=all_type)
    else:
        return [argument], argument


def flatten_list(original_list):

    """ This function flattens a list using recursion. The list can contain elements
    of any type. For example: ['a', [1, 'b']] will be flattened to ['a', 1, 'b'].
    """

    if isinstance(original_list, list):
        flattened_list = []
        for element in original_list:
            if isinstance(element, list):
                flattened_list.extend(flatten_list(element))
            else:
                flattened_list.append(element)
        return flattened_list
    else:
        raise TypeError('flatten_list() must be passed a list!')


def cast_elements_to_string(cast_list):

    """ This function casts the top level elements of a list to strings. Note that it
    does not flatten lists before doing so, so if its elements contain lists, it will
    cast these lists to strings.

    Apply flatten_list() before applying cast_elements_to_string() if you want to
    change this behavior.
    """

    if isinstance(cast_list, list):
        return [str(element) for element in cast_list]
    else:
        raise TypeError('cast_elements_to_string() must be passed a list!')


def convert_list_to_sql_tuple(filter_list):

    """ This function takes a list and formats it into a SQL list that can be used
    to filter on in the WHERE clause. For example, ['a', 'b'] becomes ('a', 'b') and
    can be applied as: SELECT * FROM table WHERE column IN ('a', 'b').

    Note that any pre-formatting on the list has to happen before it is passed to this
    function. Typical steps can include flattening lists and/or casting all elements to
    the same type.
    """

    if isinstance(filter_list, list):
        return str(filter_list).replace('[', '(').replace(']', ')')
    else:
        raise TypeError('convert_list_to_sql_tuple() must be passed a list!')


def safe_convert_list_to_sql_tuple(filter_list):

    """ Safe version of convert_list_to_sql_tuple(), by first flattening the input list
    and then casting all its elements to strings, before proceeding with the conversion.
    """

    return convert_list_to_sql_tuple(cast_elements_to_string(flatten_list(filter_list)))


def try_parse_json(text):

    """ If our Analytics Cloud Endpoint is used, events will be written in Google Cloud Storage
    as newline delimited JSON files. If however the endpoint is changed, it might no longer
    write as newline delimited, but normal JSON. Hence we try to parse this as well before giving up.

    The function returns a list which contains as its first element a boolean indicating whether
    the operation succeeded, and either the loaded JSON if the first element is true,
    or the error message if false.
    """

    # First, try to parse as newline delimited JSON:
    try:
        result = []
        for json_event in text.split('\n'):
            result.append(json.loads(json_event))
        return [True, result]

    # Second, try to parse as normal JSON list or dict:
    except Exception:
        try:
            result = json.loads(text)
            # If dict nest in list:
            if isinstance(result, dict):
                result = [result]
            return [True, result]

        # Otherwise, fail:
        except Exception as e:
            result = 'Error: {e} -- The following string could not be parsed as JSON: {text}'.format(e=e, text=text)
            return [False, [result]]


def get_dict_value(event_dict, *argv):

    """ This function takes as its first argument a dictionary, and afterwards any number of potenial
    keys (including none) to try to get a value for. The order of the potential keys matters, because
    as soon as any key yields a value it will return it (and quit). If none of the tried keys have
    an associated value, it will return None.
    """

    for arg in argv:
        value = event_dict.get(arg, None)
        if value:
            return value

    return None


def cast_to_unix_timestamp(timestamp, timestamp_format_list):

    """ This function takes a timestamp and ensures a unix timestamp is returned,
    or None otherwise.

    An integer or float is returned as-is, whereas a timestamp in human readable
    string format is parsed using the provided timestamp format(s), verified to
    be valid & finally converted into a unix timestamp & returned.
    """

    # If timestamp is already in unix time, return as-is:
    if isinstance(timestamp, (int, float)):
        return timestamp

    # If timestamp is in human readable string format, try to parse using the given
    # formats, extract the unix timestamp if the timestamp is valid & return it:
    timestamp_list = []
    if isinstance(timestamp, str):
        for format in timestamp_format_list:
            try:
                return datetime.datetime.strptime(timestamp, format)
            except ValueError:
                continue
    return None


def cast_object_to_string(object, object_type):

    """ Whenever object_type is either a list or dictionary, apply json.dumps()
    to cast it to a string, otherwise, apply str().
    """

    if object_type in [list, dict]:
        return json.dumps(object)
    else:
        return str(object)


def format_event_list(event_list, element_type, job_name, gspath):

    """ Before writing either a log or debug message to BigQuery, we must format the event by
    attaching some metadata to it, and ensure we are writing the message (event) itself as a
    proper JSON string, if it was either a list or dictionary.
    """

    new_list = [
      {'job_name': job_name,
       'processed_timestamp': time.time(),
       'batch_id': hashlib.md5(gspath.encode('utf-8')).hexdigest(),
       'analytics_environment': parse_gspath(gspath, 'analytics_environment='),
       'event_category': parse_gspath(gspath, 'event_category='),
       'event_ds': parse_gspath(gspath, 'event_ds='),
       'event_time': parse_gspath(gspath, 'event_time='),
       'event': cast_object_to_string(event, element_type),
       'gspath': gspath} for event in event_list]
    return new_list


def gunzip_bytes_obj(bytes_obj):

    """ This function unzips a gzip byte string. It is used as a fallback mechanism
    whenever decompressive transcoding is not functioning.

    More info: https://cloud.google.com/storage/docs/transcoding#decompressive_transcoding
    """

    binary_stream = io.BytesIO()
    binary_stream.write(bytes_obj)
    binary_stream.seek(0)

    with gzip.GzipFile(fileobj=binary_stream, mode='rb') as f:
        gunzipped_bytes_obj = f.read()

    return gunzipped_bytes_obj.decode('utf-8')
