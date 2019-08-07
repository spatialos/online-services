import datetime
import json
import time

def generate_date_range(ds_start, ds_stop):
    start = datetime.datetime.strptime(ds_start, '%Y-%m-%d')
    end = datetime.datetime.strptime(ds_stop, '%Y-%m-%d')
    step = datetime.timedelta(days=1)
    while start <= end:
        yield str(start.date())
        start += step


def generate_gcs_file_list(datesGenerator, ds_start, ds_stop, gcs_bucket, list_env, event_category, list_time_part, scale_test_name=''):
    for env in list_env:
        for ds in generate_date_range(ds_start, ds_stop):
            for time_part in list_time_part:
                yield 'gs://{gcs_bucket}/data_type=json/analytics_environment={env}/event_category={event_category}/event_ds={ds}/event_time={time_part}/{scale_test_name}'.format(
                  gcs_bucket=gcs_bucket, env=env, event_category=event_category, ds=ds, time_part=time_part, scale_test_name=scale_test_name)


def validate_date(date):
    try:
        datetime.datetime.strptime(date, '%Y-%m-%d')
        return True
    except ValueError:
        return False


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


def parse_analytics_environment(environment):
    if environment in ['all', '']:
        list_env, name_env = ['testing', 'development', 'staging', 'production', 'live'], 'all-envs'
    else:
        list_env, name_env = [environment], environment
    return list_env, name_env


def parse_event_time(time_part):

    """ This function parses a time part argument, which currently must be one of
    {'all', '0-8', '8-16', '16-24'}. The first option, 'all', is used to return all time parts,
    otherwise it will return the time part itself (if it is part of the verification list).

    The Analytics Cloud Endpoint is currently pre-configured to automatically determine
    (if not overridden) which one of these 3 UTC time parts it must use based on the
    UTC time when the events arrived, when setting the file's location in Google Cloud storage:

    gs://[your Google project id]-analytics/data_type=json/.../time_part=0-8/...
    """

    if time_part not in ['all', '0-8', '8-16', '16-24']:
        raise Exception("event-time argument must be one of: {'all', '0-8', '8-16', '16-24'}")

    if time_part == 'all':
        time_part_list, name_time = ['0-8', '8-16', '16-24'], '{time_part}-times'.format(time_part=time_part)
    else:
        time_part_list, name_time = [time_part], time_part
    return time_part_list, name_time


def flatten_list(original_list):
    if isinstance(original_list, list):
        flattened_list = []
        for element in original_list:
            if isinstance(element,list): flattened_list.extend(flatten_list(element))
            else: flattened_list.append(element)
        return flattened_list
    else:
        raise TypeError('flatten_list() must be passed a list!')


def cast_elements_to_string(cast_list):
    if isinstance(cast_list, list):
        return [str(element) for element in cast_list]
    else:
        raise TypeError('cast_elements_to_string() must be passed a list!')


def convert_list_to_sql_tuple(filter_list):
    if isinstance(filter_list, list):
        return str(cast_elements_to_string(flatten_list(filter_list))).replace('[', '(').replace(']', ')')
    else:
        raise TypeError('convert_list_to_sql_tuple() must be passed a list!')


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
    keys to try to get a value for. The order of the potential keys matters, because as soon as any key
    yields a value it will return it (and quit). If none of the tried keys have an associated value, it will
    return None.
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
    string format is parsed using the provided timestamp format(s), verified to be valid
    & finally converted into a unix timestamp & returned.
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


def cast_to_string(element):
    if isinstance(element, (list, dict)):
        return json.dumps(element)
    else:
        return str(element)
