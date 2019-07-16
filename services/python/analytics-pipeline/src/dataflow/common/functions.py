import datetime
import json


def datesGenerator(ds_start, ds_stop):
    import datetime
    start = datetime.datetime.strptime(ds_start, '%Y-%m-%d')
    end = datetime.datetime.strptime(ds_stop, '%Y-%m-%d')
    step = datetime.timedelta(days=1)
    while start <= end:
        yield str(start.date())
        start += step


def gcsFileListGenerator(datesGenerator, ds_start, ds_stop, gcs_bucket, list_env, event_category, list_time_part, scale_test_name=''):
    for env in list_env:
        for ds in datesGenerator(ds_start, ds_stop):
            for time_part in list_time_part:
                yield 'gs://{gcs_bucket}/data_type=json/analytics_environment={env}/event_category={event_category}/event_ds={ds}/event_time={time_part}/{scale_test_name}'.format(
                  gcs_bucket=gcs_bucket, env=env, event_category=event_category, ds=ds, time_part=time_part, scale_test_name=scale_test_name)


def pathParser(path, key):
    try:
        value = path.split(key)[1].split('/')[0]
        if key == 'event_ds=':
            try:
                x = datetime.datetime.strptime(value, '%Y-%m-%d')
            except Exception:
                value = None
    except Exception:
        value = None
    return value


def envParser(environment):
    if environment in ['all', '']:
        list_env, name_env = ['testing', 'development', 'staging', 'production', 'live'], 'all-envs'
    else:
        list_env, name_env = [environment], environment
    return list_env, name_env


def timeParser(time_part):
    if time_part == 'all':
        list_time_part, name_time = ['0-8', '8-16', '16-24'], time_part + '-times'
    else:
        list_time_part, name_time = [time_part], time_part
    return list_time_part, name_time


def listToSqlTuple(_list):
    if isinstance(_list, list):
        return str(_list).replace('[', '(').replace(']', ')')
    else:
        raise Exception('listToSqlTuple did not receive a list!')


def jsonParser(text):
    # First, try to parse as JSON list or dict
    try:
        x = json.loads(text)
    except Exception:
        # Second, try to parse as newline delimited JSON
        try:
            x = []
            for i in text.split('\n'):
                x.append(json.loads(i))
        # Otherwise, fail
        except Exception as e:
            x = 'Error: {e} -- The following string could not be parsed as JSON: {text}'.format(e=e, text=text)
    return x


def parseField(_dict, option1, option2=''):
    try:
        v = _dict[option1]
    except Exception:
        try:
            v = _dict[option2]
        except Exception:
            v = None
    return v


def unixTimestampCheck(ts):
    from datetime import datetime
    import time

    if isinstance(ts, float) or isinstance(ts, int):
        return ts

    ts_list = []
    if isinstance(ts, str):
        for i in ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z']:
            try:
                d = datetime.strptime(ts, i)
                ts_list.append(time.mktime(d.timetuple()))
            except Exception:
                pass
    ts_list.append(None)
    return ts_list[0]
