// <copyright file="realmSubjectIndexes.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';

export const fetchRealmSubjectIndexConflicts = ({ realmId }) =>
  api.get(`/realm-subject-indexes/${encodeURIComponent(realmId)}/conflicts`);

export const summarizeRealmSubjectIndexConflicts = (report = {}) => {
  const conflicts = report.conflicts || report.Conflicts || [];
  const values = conflicts.flatMap((conflict) => conflict.values || conflict.Values || []);
  const authorityKeys = new Set(
    values
      .map((value) => value.authorityKey || value.AuthorityKey)
      .filter(Boolean),
  );
  const conflictTypes = new Set(
    conflicts.map((conflict) => conflict.type || conflict.Type).filter(Boolean),
  );

  return {
    authorityCount: authorityKeys.size,
    conflictCount: conflicts.length,
    conflictTypeCount: conflictTypes.size,
    entryCount: report.entryCount ?? report.EntryCount ?? 0,
    indexCount: report.indexCount ?? report.IndexCount ?? 0,
  };
};

export const formatRealmSubjectIndexConflictReport = ({
  disabledAuthorities = [],
  report = {},
} = {}) => {
  const conflicts = report.conflicts || report.Conflicts || [];
  const lines = [
    'slskdN realm subject-index conflict review',
    `Realm: ${report.realmId || report.RealmId || '-'}`,
    `Indexes: ${report.indexCount ?? report.IndexCount ?? 0}`,
    `Entries: ${report.entryCount ?? report.EntryCount ?? 0}`,
    `Conflicts: ${conflicts.length}`,
    `Locally disabled authorities: ${disabledAuthorities.length}`,
    '',
  ];

  conflicts.forEach((conflict) => {
    lines.push(
      `Conflict: ${conflict.type || conflict.Type || 'unknown'} / ${
        conflict.key || conflict.Key || '-'
      }`,
    );
    lines.push(`Subject: ${conflict.subjectId || conflict.SubjectId || '-'}`);
    lines.push(`Description: ${conflict.description || conflict.Description || '-'}`);
    (conflict.values || conflict.Values || []).forEach((value) => {
      const authorityKey = value.authorityKey || value.AuthorityKey || '-';
      lines.push(
        `- ${disabledAuthorities.includes(authorityKey) ? 'DISABLED' : 'ACTIVE'} ${authorityKey}: ${
          value.value || value.Value || '-'
        }`,
      );
      if (value.provenance || value.Provenance) {
        lines.push(`  Provenance: ${value.provenance || value.Provenance}`);
      }
    });
    lines.push('');
  });

  return lines.join('\n').trim();
};
