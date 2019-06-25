# -*- coding: utf-8 -*-
# Python 3.6.5

from google.cloud import bigquery, storage
import hashlib
import base64
import json
import time
import os

from common.parser import jsonParser, parseField, pathParser, unixTimestampCheck
from common.bigquery import provisionBigQuery

# Function acts as function-gcs-to-bq@[your project id].iam.gserviceaccount.com
client_gcs, client_bq = storage.Client(), bigquery.Client()

def elementCast(element):
	if isinstance(element, dict):
		return json.dumps(element)
	else:
		return element

def eventFormatter(list, job_name, gspath):
	new_list = [{'job_name': job_name, 'processed_timestamp': time.time(), 'batch_id': hashlib.md5('/'.join(gspath.split('/')[-2:]).encode('utf-8')).hexdigest(),
	'analytics_environment': pathParser(gspath, 'analytics_environment='), 'event_category': pathParser(gspath, 'event_category='), 'event_ds': pathParser(gspath, 'event_ds='),
	'event_time': pathParser(gspath, 'event_time='), 'event': elementCast(i), 'file_path': gspath} for i in list]
	return new_list

def sourceBigQuery():
	dataset_logs_ref, dataset_events_ref = client_bq.dataset('logs'), client_bq.dataset('events')
	table_logs_ref, table_debug_ref, table_function_ref = dataset_logs_ref.table('events_logs_function'), dataset_logs_ref.table('events_debug_function'), dataset_events_ref.table('events_function')
	return client_bq.get_table(table_logs_ref), client_bq.get_table(table_debug_ref), client_bq.get_table(table_function_ref)

def cf0GcsToBq(data, context):
	job_name = 'cloud-function'

	# Source required datasets & tables:
	try:
		table_logs, table_debug, table_function = sourceBigQuery()
	except:
		_ = provisionBigQuery(client_bq, 'function', True)
		table_logs, table_debug, table_function = sourceBigQuery()

	# Parse payload:
	payload = json.loads(base64.b64decode(data['data']).decode('utf-8'))
	bucket, name = payload['bucket'], payload['name']
	gspath = 'gs://{bucket}/{name}'.format(bucket = bucket, name = name)

	# Write log to events_logs_function:
	errors = client_bq.insert_rows(table_logs, eventFormatter(['parse_initiated'], job_name, gspath))
	if errors:
		print('Errors while inserting logs: ' + str(errors))

	# Get file from GCS:
	_bucket = client_gcs.get_bucket(bucket)
	batch = jsonParser(_bucket.get_blob(name).download_as_string().decode('utf8'))

	# If dict nest in list:
	if isinstance(batch, dict):
		batch = [batch]

	# Parse list:
	if isinstance(batch, list):
		batch_function, batch_debug = [], []
		for event in batch:
			d = {}
			# Sanitize:
			d['event_class'] = parseField(_dict = event, option1 = ['eventClass'], option2 = ['event_class'])
			if d['event_class'] is not None:
				d['analytics_environment'] = parseField(_dict = event, option1 = ['analyticsEnvironment'], option2 = ['analytics_environment'])
				d['batch_id'] = parseField(_dict = event, option1 = ['batchId'], option2 = ['batch_id'])
				d['event_id'] = parseField(_dict = event, option1 = ['eventId'], option2 = ['event_id'])
				d['event_index'] = parseField(_dict = event, option1 = ['eventIndex'], option2 = ['event_index'])
				d['event_source'] = parseField(_dict = event, option1 = ['eventSource'], option2 = ['event_source'])
				d['event_type'] = parseField(_dict = event, option1 = ['eventType'], option2 = ['event_type'])
				d['session_id'] = parseField(_dict = event, option1 = ['sessionId'], option2 = ['session_id'])
				d['build_version'] = parseField(_dict = event, option1 = ['buildVersion'], option2 = ['build_version'])
				d['event_environment'] = parseField(_dict = event, option1 = ['eventEnvironment'], option2 = ['event_environment'])
				d['event_timestamp'] = unixTimestampCheck(parseField(_dict = event, option1 = ['eventTimestamp'], option2 = ['event_timestamp']))
				d['received_timestamp'] = unixTimestampCheck(parseField(_dict = event, option1 = ['receivedTimestamp'], option2 = ['received_timestamp']))
				# Augment:
				d['inserted_timestamp'] = time.time()
				d['job_name'] = os.environ['FUNCTION_NAME']
				# Sanitize:
				d['event_attributes'] = parseField(_dict = event, option1 = ['eventAttributes'], option2 = ['event_attributes'])
				batch_function.append(d)
			else:
				batch_debug.append(event)

		if len(batch_function) > 0:
			# Write session JSON to events_function:
			errors = client_bq.insert_rows(table_function, batch_function)
			if errors:
				print('Errors while inserting events: ' + str(errors))

		if len(batch_debug) > 0:
			# Write non-session JSON to events_debug_function:
			errors = client_bq.insert_rows(table_debug, eventFormatter(batch_debug, job_name, gspath))
			if errors:
				print('Errors while inserting events: ' + str(errors))

	else:
		# Write non-JSON to debugSink:
		errors = client_bq.insert_rows(table_debug, eventFormatter([batch], job_name, gspath))
		if errors:
			print('Errors while inserting debug event: ' + str(errors))
